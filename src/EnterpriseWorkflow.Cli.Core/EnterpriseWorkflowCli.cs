using System.Reflection;
using System.Text.RegularExpressions;
using EnterpriseWorkflow.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EnterpriseWorkflow.Cli;

public static class EnterpriseWorkflowCli
{
    public static EnterpriseWorkflowCliBuilder CreateBuilder(string[] args) => new(args);
}

public sealed record WorkflowCliContext(string[] Arguments, IConfiguration Configuration,
    IServiceProvider Services, TextWriter Output, TextWriter Error, ILogger Logger, CancellationToken CancellationToken);

public sealed record WorkflowDevelopmentContext(IConfiguration Configuration);

/// <summary>Options include their leading dashes. Value options consume the next argument.</summary>
public sealed record WorkflowCliOption(string Name, bool RequiresValue = false);

public sealed class EnterpriseWorkflowCliBuilder
{
    private readonly string[] _args;
    private readonly Dictionary<string, WorkflowCliCommand> _commands = new(StringComparer.Ordinal);
    private readonly List<Action<WorkflowDevelopmentContext, IServiceCollection>> _development = [];
    private readonly List<Action<WorkflowModuleLoadOptions>> _modules = [];
    private readonly Dictionary<string, string[]> _defaults = new(StringComparer.Ordinal);
    private string _name = "ew";
    private bool _built;
    private WorkflowTemplatePackage? _templates;
    internal EnterpriseWorkflowCliBuilder(string[] args)
    { _args = (string[])args.Clone(); Configuration.AddEnvironmentVariables(); }
    public ConfigurationManager Configuration { get; } = new();
    public IServiceCollection Services { get; } = new ServiceCollection();

    public EnterpriseWorkflowCliBuilder UseCommandName(string name)
    {
        EnsureMutable();
        if (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9-]*$", RegexOptions.CultureInvariant)) throw new ArgumentException("Invalid command name.", nameof(name));
        _name = name; return this;
    }
    public EnterpriseWorkflowCliBuilder AddStandardCommands() { EnsureMutable(); return this; }
    public EnterpriseWorkflowCliBuilder ConfigureModules(Action<WorkflowModuleLoadOptions> configure)
    { EnsureMutable(); ArgumentNullException.ThrowIfNull(configure); _modules.Add(configure); return this; }
    public EnterpriseWorkflowCliBuilder ConfigureDevelopmentServices(Action<WorkflowDevelopmentContext, IServiceCollection> configure)
    { EnsureMutable(); ArgumentNullException.ThrowIfNull(configure); _development.Add(configure); return this; }
    public EnterpriseWorkflowCliBuilder AddCommand(string name, string description, Func<WorkflowCliContext, Task<int>> execute, params WorkflowCliOption[] options)
        => Register(name, description, execute, false, options);
    public EnterpriseWorkflowCliBuilder ReplaceCommand(string name, string description, Func<WorkflowCliContext, Task<int>> execute, params WorkflowCliOption[] options)
        => Register(name, description, execute, true, options);
    public EnterpriseWorkflowCliBuilder ConfigureCommandDefaults(string command, params string[] arguments)
    {
        EnsureMutable();
        if (!WorkflowCliExecution.StandardCommands.Contains(command) && !_commands.ContainsKey(command)) throw new ArgumentException("Unknown command.", nameof(command));
        if (arguments.Length == 0 || !arguments[0].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Defaults must start with an option.", nameof(arguments));
        _defaults[command] = (string[])arguments.Clone(); return this;
    }
    public EnterpriseWorkflowCliBuilder AddTemplates(string packageId, string version, params string[] shortNames)
    {
        EnsureMutable();
        if (_templates is not null || _commands.ContainsKey("setup") || _commands.ContainsKey("new")) throw new InvalidOperationException("Template commands already registered.");
        _templates = new(packageId, version, shortNames);
        return this;
    }
    private EnterpriseWorkflowCliBuilder Register(string name, string description, Func<WorkflowCliContext, Task<int>> execute, bool replace, WorkflowCliOption[] options)
    {
        EnsureMutable(); ArgumentNullException.ThrowIfNull(execute);
        if (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9-]*( [a-zA-Z][a-zA-Z0-9-]*)*$", RegexOptions.CultureInvariant) || name.Split(' ')[0] is "setup" or "new" || (name.Contains(' ') && WorkflowCliExecution.StandardCommands.Contains(name.Split(' ')[0]))) throw new ArgumentException("Reserved or invalid command.", nameof(name));
        var exists = _commands.ContainsKey(name) || WorkflowCliExecution.StandardCommands.Contains(name);
        if (exists != replace) throw new InvalidOperationException(exists ? "Command already exists; use ReplaceCommand explicitly." : "Cannot replace an unknown command.");
        if (options.Any(item => !item.Name.StartsWith("--", StringComparison.Ordinal) || item.Name is "--help") || options.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != options.Length)
            throw new ArgumentException("Invalid, reserved or duplicate option.", nameof(options));
        _commands[name] = new(description, execute, (WorkflowCliOption[])options.Clone()); return this;
    }
    public EnterpriseWorkflowCliApplication Build()
    {
        EnsureMutable(); _built = true;
        return new(_args, _name, Configuration, Services, new(_commands, StringComparer.Ordinal), _development.ToArray(), _modules.ToArray(), _templates, _defaults);
    }
    private void EnsureMutable() { if (_built) throw new InvalidOperationException("The CLI builder has already been built."); }
}

internal sealed record WorkflowCliCommand(string Description, Func<WorkflowCliContext, Task<int>> Execute, WorkflowCliOption[] Options);

public sealed class EnterpriseWorkflowCliApplication
{
    private readonly string[] _args;
    private readonly string _name;
    private readonly IConfiguration _configuration;
    private readonly ServiceDescriptor[] _services;
    private readonly Dictionary<string, WorkflowCliCommand> _commands;
    private readonly Action<WorkflowDevelopmentContext, IServiceCollection>[] _development;
    private readonly Action<WorkflowModuleLoadOptions>[] _modules;
    private readonly WorkflowTemplatePackage? _templates;
    internal EnterpriseWorkflowCliApplication(string[] args, string name, IConfiguration configuration, IServiceCollection services,
        Dictionary<string, WorkflowCliCommand> commands, Action<WorkflowDevelopmentContext, IServiceCollection>[] development,
        Action<WorkflowModuleLoadOptions>[] modules, WorkflowTemplatePackage? templates, Dictionary<string, string[]> defaults)
    {
        _args = ApplyDefaults(args, defaults); _name = name; _configuration = new ConfigurationBuilder().AddInMemoryCollection(configuration.AsEnumerable()).Build();
        _services = services.ToArray(); _commands = commands; _development = development; _modules = modules; _templates = templates;
    }
    private static string[] ApplyDefaults(string[] args, Dictionary<string, string[]> defaults)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal)) return args;
        var match = defaults.OrderByDescending(pair => pair.Key.Split(' ').Length).FirstOrDefault(pair =>
            args.Length >= pair.Key.Split(' ').Length && args.Take(pair.Key.Split(' ').Length).SequenceEqual(pair.Key.Split(' '), StringComparer.Ordinal));
        if (match.Value is not { } values) return args;
        var merged = args.ToList();
        for (var index = 0; index < values.Length; index++)
        {
            var option = values[index];
            if (!option.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Expected an option in command defaults.");
            var value = index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal) ? values[++index] : null;
            if (args.Contains(option, StringComparer.Ordinal)) continue;
            merged.Add(option); if (value is not null) merged.Add(value);
        }
        return merged.ToArray();
    }
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        using var execution = WorkflowCliExecution.Enter(_development, _modules, cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_args.Length == 0 || _args[0] is "--help" or "-h")
            {
                Console.WriteLine(WorkflowCli.Help.Replace("ew ", _name + " ", StringComparison.Ordinal));
                foreach (var pair in _commands) Console.WriteLine($"{_name} {pair.Key} {string.Join(' ', pair.Value.Options.Select(option => option.Name + (option.RequiresValue ? " <value>" : "")))} — {pair.Value.Description}");
                if (_templates is not null) Console.WriteLine($"{_name} setup\n{_name} new <template> [dotnet new options]");
                return 0;
            }
            if (_templates is not null && _args[0] is "setup" or "new") return await _templates.RunAsync(_name, _args, _configuration, cancellationToken);
            var match = _commands.OrderByDescending(pair => pair.Key.Split(' ').Length).FirstOrDefault(pair =>
                _args.Length >= pair.Key.Split(' ').Length && _args.Take(pair.Key.Split(' ').Length).SequenceEqual(pair.Key.Split(' '), StringComparer.Ordinal));
            if (match.Value is not { } command) return await WorkflowCliEntry.RunAsync(_args);
            var arguments = _args[match.Key.Split(' ').Length..];
            if (arguments is ["--help"] or ["-h"]) { Console.WriteLine(command.Description); return 0; }
            for (var index = 0; index < arguments.Length; index++)
            {
                if (!arguments[index].StartsWith('-')) continue;
                var option = command.Options.SingleOrDefault(item => item.Name == arguments[index]) ?? throw new ArgumentException($"Unknown option '{arguments[index]}'.");
                if (option.RequiresValue && ++index >= arguments.Length) throw new ArgumentException($"Missing value for {option.Name}.");
            }
            var services = new ServiceCollection();
            foreach (var descriptor in _services) ((IServiceCollection)services).Add(descriptor);
            services.AddLogging(); services.AddSingleton(_configuration);
            await using var provider = services.BuildServiceProvider();
            return await command.Execute(new(arguments, _configuration, provider, Console.Out, Console.Error,
                provider.GetRequiredService<ILoggerFactory>().CreateLogger(_name), cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return 130; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        { Console.Error.WriteLine(exception.Message); return 1; }
    }
}

internal sealed class WorkflowCliExecution : IDisposable
{
    private static readonly AsyncLocal<WorkflowCliExecution?> Current = new();
    internal static readonly HashSet<string> StandardCommands = new(StringComparer.Ordinal) { "validate", "inspect", "pack", "verify", "dev" };
    private readonly WorkflowCliExecution? _previous;
    private readonly Action<WorkflowDevelopmentContext, IServiceCollection>[] _development;
    private readonly Action<WorkflowModuleLoadOptions>[] _modules;
    private WorkflowCliExecution(Action<WorkflowDevelopmentContext, IServiceCollection>[] development, Action<WorkflowModuleLoadOptions>[] modules, CancellationToken cancellationToken)
    { Token = cancellationToken; _previous = Current.Value; _development = development; _modules = modules; Current.Value = this; }
    internal static WorkflowCliExecution Enter(Action<WorkflowDevelopmentContext, IServiceCollection>[] development, Action<WorkflowModuleLoadOptions>[] modules, CancellationToken cancellationToken = default) => new(development, modules, cancellationToken);
    private CancellationToken Token { get; }
    internal static CancellationToken CancellationToken => Current.Value?.Token ?? default;
    internal static string EntryAssemblyPath => Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("A compiled CLI entry assembly is required.");
    internal static void ConfigureModules(WorkflowModuleLoadOptions options)
    { foreach (var configure in Current.Value?._modules ?? []) configure(options); }
    internal static void ConfigureDevelopment(IConfiguration configuration, IServiceCollection services)
    {
        var baseline = services.ToArray();
        foreach (var configure in Current.Value?._development ?? []) configure(new(configuration), services);
        var reserved = baseline.Select(item => item.ServiceType).ToHashSet();
        var removed = baseline.FirstOrDefault(item => !services.Contains(item));
        var forbidden = services.Except(baseline).FirstOrDefault(item => IsSecurityService(item.ServiceType) ||
            (reserved.Contains(item.ServiceType) && !IsAdditiveOptionsService(item.ServiceType)));
        if (removed is not null || forbidden is not null)
            throw new InvalidOperationException($"Development extensions cannot replace existing services or register authentication/security providers. Service: {(removed ?? forbidden)!.ServiceType}.");
    }
    private static bool IsAdditiveOptionsService(Type type) => type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Options.IConfigureOptions<>) ||
         type.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Options.IPostConfigureOptions<>) ||
         type.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Options.IValidateOptions<>)) &&
        type.GenericTypeArguments.All(argument =>
            argument.Namespace?.StartsWith("EnterpriseWorkflow", StringComparison.Ordinal) != true &&
            argument.Namespace?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) != true);

    private static bool IsSecurityService(Type type) =>
        type.Namespace?.StartsWith("EnterpriseWorkflow.Security", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("Microsoft.AspNetCore.Authentication", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("Microsoft.AspNetCore.Antiforgery", StringComparison.Ordinal) == true ||
        type.GenericTypeArguments.Any(IsSecurityService);

    public void Dispose() => Current.Value = _previous;
}

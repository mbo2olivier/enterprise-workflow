using System.Diagnostics;
using NuGet.Versioning;

namespace EnterpriseWorkflow.Cli;

internal sealed class WorkflowTemplatePackage
{
    private readonly string _package;
    private readonly string _version;
    private readonly string[] _shortNames;
    internal WorkflowTemplatePackage(string package, string version, string[] shortNames)
    {
        if (string.IsNullOrWhiteSpace(package) || package.Length > 100 || !char.IsAsciiLetterOrDigit(package[0]) || package.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_')) throw new ArgumentException("Invalid template package ID.", nameof(package));
        if (!NuGetVersion.TryParse(version, out _) || shortNames.Length == 0 || shortNames.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("An exact template version and short names are required.", nameof(version));
        _package = package; _version = version; _shortNames = (string[])shortNames.Clone();
    }
    internal async Task<int> RunAsync(string name, string[] args, Microsoft.Extensions.Configuration.IConfiguration configuration, CancellationToken cancellationToken)
    {
        // Separate engine hive per exact distribution package/version: never use a marker as proof of installation.
        var hive = Path.Combine(configuration["Templates:Root"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnterpriseWorkflow", "templates"), _package, _version);
        string[] command;
        if (args is ["setup"])
            command = ["new", "install", _package + "::" + _version, "--debug:custom-hive", hive];
        else if (args.Length >= 2 && args[0] == "new" && _shortNames.Contains(args[1], StringComparer.Ordinal))
        {
            if (args[2..].Any(argument => argument.StartsWith("--debug:", StringComparison.Ordinal) || argument is "--install" or "--uninstall")) throw new ArgumentException("Template engine management options are reserved.");
            command = ["new", args[1], "--debug:custom-hive", hive, "--no-update-check", .. args[2..]];
        }
        else throw new ArgumentException($"Use {name} setup or {name} new <{string.Join('|', _shortNames)}>.");
        var result = await RunDotnetAsync(command, cancellationToken);
        // dotnet new <missing-template> --help may return 0. The real generation
        // returns the documented template-not-found code instead.
        if (args[0] == "new" && result.Code == 103)
            throw new InvalidOperationException($"Templates are not prepared. Run {name} setup.");
        return result.Code;
    }
    private static async Task<(int Code, string Output)> RunDotnetAsync(string[] args, CancellationToken cancellationToken, bool capture = false)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = capture, RedirectStandardError = capture };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start dotnet.");
        var output = capture ? process.StandardOutput.ReadToEndAsync(cancellationToken) : Task.FromResult("");
        var error = capture ? process.StandardError.ReadToEndAsync(cancellationToken) : Task.FromResult("");
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output + await error);
    }
}

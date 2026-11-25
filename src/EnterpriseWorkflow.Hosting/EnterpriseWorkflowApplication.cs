using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence;
using EnterpriseWorkflow.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnterpriseWorkflow.Hosting;

public static class EnterpriseWorkflowApplication
{
    public static EnterpriseWorkflowApplicationBuilder CreateBuilder(string[] args) => new(Host.CreateApplicationBuilder(args));
    public static EnterpriseWorkflowApplicationBuilder CreateWebBuilder(string[] args) => new(WebApplication.CreateBuilder(args));

    /// <summary>Preserves the shipped kernel's base-directory configuration and explicit command arguments.</summary>
    public static EnterpriseWorkflowApplicationBuilder CreateKernelBuilder()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Production;
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            Args = [], ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? typeof(EnterpriseWorkflowWebExtensions).Assembly.GetName().Name,
            EnvironmentName = environment,
        });
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), true, false)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{environment}.json"), true, false)
            .AddEnvironmentVariables().AddEnvironmentVariables("DOTNET_").AddEnvironmentVariables("ASPNETCORE_");
        builder.Logging.AddSimpleConsole();
        builder.WebHost.UseKestrel();
        Microsoft.AspNetCore.Hosting.StaticWebAssets.StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        return new(builder);
    }
}

public sealed class EnterpriseWorkflowApplicationBuilder
{
    private readonly IHostApplicationBuilder _builder;
    private WorkflowDatabaseRuntime? _database;
    private readonly WorkflowBrandingOptions _branding = new();
    private bool _web;
    private bool _built;
    private readonly List<Action<WorkflowRuntimeOptions>> _runtimeConfiguration = [];
    private readonly List<Action<WorkflowPortalOptions>> _presentationConfiguration = [];
    private readonly List<Action<WorkflowModuleLoadOptions>> _moduleConfiguration = [];
    private readonly List<Action<WorkflowModuleDiscoveryOptions>> _discoveryConfiguration = [];
    private readonly List<Action<WorkflowHandlerRegistryBuilder>> _handlerConfiguration = [];
    internal EnterpriseWorkflowApplicationBuilder(IHostApplicationBuilder builder) => _builder = builder;
    public ConfigurationManager Configuration => (ConfigurationManager)_builder.Configuration;
    public IServiceCollection Services => _builder.Services;
    public ILoggingBuilder Logging => _builder.Logging;
    public IHostEnvironment Environment => _builder.Environment;
    public IWebHostBuilder WebHost => (_builder as WebApplicationBuilder)?.WebHost
        ?? throw new InvalidOperationException("This builder does not host a web server.");

    public EnterpriseWorkflowApplicationBuilder ConfigureBranding(Action<WorkflowBrandingOptions> configure)
    { ArgumentNullException.ThrowIfNull(configure); configure(_branding); return this; }

    public EnterpriseWorkflowApplicationBuilder ConfigureRuntime(Action<WorkflowRuntimeOptions> configure)
    { ArgumentNullException.ThrowIfNull(configure); _runtimeConfiguration.Add(configure); return this; }
    public EnterpriseWorkflowApplicationBuilder ConfigurePresentation(Action<WorkflowPortalOptions> configure)
    { ArgumentNullException.ThrowIfNull(configure); _presentationConfiguration.Add(configure); return this; }
    public EnterpriseWorkflowApplicationBuilder ConfigureModules(Action<WorkflowModuleLoadOptions> configure)
    { ArgumentNullException.ThrowIfNull(configure); _moduleConfiguration.Add(configure); return this; }
    public EnterpriseWorkflowApplicationBuilder ConfigureDiscovery(Action<WorkflowModuleDiscoveryOptions> configure)
    { ArgumentNullException.ThrowIfNull(configure); _discoveryConfiguration.Add(configure); return this; }
    public EnterpriseWorkflowApplicationBuilder ConfigureHandlers(Action<WorkflowHandlerRegistryBuilder> configure)
    { ArgumentNullException.ThrowIfNull(configure); _handlerConfiguration.Add(configure); return this; }
    public EnterpriseWorkflowApplicationBuilder UseExternalEffectTransport<T>() where T : class, IExternalEffectTransport
    { Services.AddSingleton<IExternalEffectTransport, T>(); return this; }

    public EnterpriseWorkflowApplicationBuilder AddWebPresentation()
    {
        if (_builder is not WebApplicationBuilder) throw new InvalidOperationException("Use CreateWebBuilder to host web presentation.");
        _web = true;
        return this;
    }

    public EnterpriseWorkflowApplicationBuilder UseDatabase(WorkflowDatabaseRuntime database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
        return this;
    }

    public EnterpriseWorkflowApplicationBuilder AddAuthenticationProvider<T>() where T : class, IAuthenticationProvider
    {
        Services.TryAddSingleton<T>();
        Services.AddSingleton<IAuthenticationProvider>(provider => provider.GetRequiredService<T>());
        return this;
    }

    public EnterpriseWorkflowApplicationBuilder AddIdentityProvider<T>() where T : class
    {
        Services.TryAddSingleton<T>();
        foreach (var contract in new[] { typeof(IIdentityDirectory), typeof(IIdentityResolver), typeof(IIdentitySearchProvider),
            typeof(IIdentityStatusProvider), typeof(IIdentityAdmissionProvider), typeof(IIdentityImportProvider), typeof(IIdentityProvisioningProvider) })
            if (contract.IsAssignableFrom(typeof(T))) Services.AddSingleton(contract, provider => provider.GetRequiredService<T>());
        return this;
    }

    public EnterpriseWorkflowApplicationBuilder AddSecondFactorHandler<T>() where T : class, ISecondFactorHandler
    {
        Services.TryAddSingleton<T>();
        Services.AddSingleton<ISecondFactorHandler>(provider => provider.GetRequiredService<T>());
        return this;
    }

    public Task MigrateAsync() => WorkflowDatabaseFactory.PrepareSchemasAsync(Database, DatabaseMigrationMode.Apply);

    public async Task<ProviderResult<bool>> BootstrapAsync(string userName, string password)
    {
        await WorkflowDatabaseFactory.PrepareSchemasAsync(Database, MigrationMode);
        var policy = Configuration.GetSection("Security:Policy").Get<SecurityPolicyOptions>() ?? new();
        policy.Validate();
        var store = Database.CreateSecurityStore(TimeProvider.System);
        return await new LocalSecurityProvisioner(store, new LocalAuthenticationProvider(store, policy, TimeProvider.System))
            .BootstrapAsync(userName, password, CancellationToken.None);
    }

    private WorkflowDatabaseRuntime Database => _database ??= WorkflowDatabaseFactory.CreateDatabaseRuntime(Configuration["Database:Provider"] ?? "Sqlite", Configuration);
    private DatabaseMigrationMode MigrationMode => Enum.Parse<DatabaseMigrationMode>(Configuration["Database:MigrationMode"] ?? "Validate", true);

    public async Task<EnterpriseWorkflowHost> BuildAsync()
    {
        if (_built) throw new InvalidOperationException("The host builder can only be built once.");
        _built = true;
        _branding.Validate();
        Services.AddSingleton(_branding);
        var localization = WorkflowLocalizationOptions.FromConfiguration(Configuration);
        localization.Validate();
        Services.AddSingleton(new WorkflowTextLocalizationOptions { FallbackCulture = localization.FallbackCulture });
        var secondFactor = Configuration.GetSection("Security:SecondFactor").Get<SecondFactorOptions>() ?? new();
        ValidateSingleRegistration<IAuthenticationProvider>();
        ValidateSingleRegistration<ISecondFactorHandler>();
        if (secondFactor.Enabled && !Services.Any(item => item.ServiceType == typeof(ISecondFactorHandler)))
            throw new InvalidOperationException("Second authentication is enabled but no compiled handler is registered.");
        Services.AddSingleton(secondFactor);
        var builder = _builder;
        var database = Database;
        await WorkflowDatabaseFactory.PrepareSchemasAsync(database, MigrationMode);
        var policy = builder.Configuration.GetSection("Security:Policy").Get<SecurityPolicyOptions>() ?? new();
        policy.Validate();
        var timeProvider = TimeProvider.System;
        var securityStore = database.CreateSecurityStore(timeProvider);
        var localAuthentication = new LocalAuthenticationProvider(securityStore, policy, timeProvider);
        var moduleDiagnostics = new List<WorkflowModuleDiscoveryDiagnostic>();
        var moduleCatalog = WorkflowModuleSource.Load(builder.Configuration, builder.Environment.ContentRootPath,
            options => { foreach (var configure in _moduleConfiguration) configure(options); },
            options => { foreach (var configure in _discoveryConfiguration) configure(options); }, moduleDiagnostics.Add);
        await WorkflowModuleKernel.ReconcileAsync(moduleCatalog, database.WorkflowMaintenanceStore);

        var configuredDirectories = new Dictionary<string, IIdentityDirectory>(StringComparer.Ordinal);
        IAuthenticationProvider selectedAuthentication = localAuthentication;
        ConfigureDefaultAuthentication(builder.Configuration, builder.Services, localAuthentication, configuredDirectories);
        IReadOnlyDictionary<string, IIdentityDirectory> directories = configuredDirectories;
        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(policy);
        builder.Services.AddSingleton(database.WorkflowStore);
        builder.Services.AddSingleton<IWorkflowStore>(database.WorkflowStore);
        builder.Services.AddSingleton<IWorkflowMaintenanceStore>(database.WorkflowMaintenanceStore);
        builder.Services.AddSingleton(securityStore);
        builder.Services.AddSingleton<ISecurityProfileStore>(securityStore);
        builder.Services.AddSingleton<ILocalAccountStore>(securityStore);
        builder.Services.AddSingleton<ISecuritySessionStore>(securityStore);
        builder.Services.AddSingleton<ILocalSecurityProvisioningStore>(securityStore);
        builder.Services.AddSingleton<IWorkflowAccessStore>(securityStore);
        builder.Services.AddSingleton<IPresentationSettingsStore>(new WorkflowBrandedSettingsStore(securityStore, _branding));
                builder.Services.AddSingleton<IUserCulturePreferenceStore>(securityStore);
        builder.Services.TryAddSingleton<IReadOnlyDictionary<string, IIdentityDirectory>>(services =>
            CreateDirectories(services, configuredDirectories));
        builder.Services.AddSingleton(localAuthentication);
        builder.Services.TryAddSingleton<IAuthenticationProvider>(selectedAuthentication);
        builder.Services.AddSingleton<SecuritySessionManager>();
        builder.Services.AddSingleton<IAuthorizationProvider>(services =>
            new IdentityAdmissionAuthorizationProvider(
                new InternalProfileAuthorizationProvider(services.GetRequiredService<ISecurityProfileStore>(), services.GetRequiredService<IReadOnlyDictionary<string, IIdentityDirectory>>()), services.GetServices<IIdentityAdmissionProvider>()));
        builder.Services.AddSingleton<IWorkflowAuthorizationService>(services =>
            new IdentityAdmissionWorkflowAuthorizationService(new ModulePermissionAuthorizationService(services.GetRequiredService<IWorkflowPresentationCatalog>(),
                services.GetRequiredService<IAuthorizationProvider>(),
                new InternalWorkflowAuthorizationService(services.GetRequiredService<IWorkflowAccessStore>(), services.GetRequiredService<IReadOnlyDictionary<string, IIdentityDirectory>>())), services.GetServices<IIdentityAdmissionProvider>()));
        builder.Services.AddSingleton<IWorkflowActionCatalog>(services =>
        {
            var presentation = services.GetRequiredService<IWorkflowPresentationCatalog>();
            var descriptors = presentation.Processes.Where(process => WorkflowStagePolicy.Read(
                process.Definition.Nodes.Single(node => node.Role == EnterpriseWorkflow.Core.Model.WorkflowNodeRole.Start).Configuration) is null).SelectMany(process =>
            {
                var global = new[]
                {
                    new WorkflowActionDescriptor(new(process.Definition.Id.Value, process.Definition.Version, null, WorkflowActions.Start)),
                    new WorkflowActionDescriptor(new(process.Definition.Id.Value, process.Definition.Version, null, WorkflowActions.ReadInstance)),
                    new WorkflowActionDescriptor(new(process.Definition.Id.Value, process.Definition.Version, null, WorkflowActions.CancelInstance)),
                };
                var tasks = process.Definition.Nodes.Where(node => node.HumanTask is not null).SelectMany(node =>
                    node.HumanTask!.Actions.Select(action => new WorkflowActionDescriptor(new(
                        process.Definition.Id.Value, process.Definition.Version, node.Id.Value,
                        new WorkflowActionId(action.ActionId.Value))))
                    .Append(new(new(process.Definition.Id.Value, process.Definition.Version, node.Id.Value, WorkflowActions.ReadTask)))
                    .Append(new(new(process.Definition.Id.Value, process.Definition.Version, node.Id.Value, WorkflowActions.ClaimTask)))
                    .Append(new(new(process.Definition.Id.Value, process.Definition.Version, node.Id.Value, WorkflowActions.AssignTask))));
                return global.Concat(tasks);
            });
            return new WorkflowActionCatalog(descriptors);
        });
        builder.Services.AddSingleton<WorkflowAccessAdministration>();
        builder.Services.AddSingleton<LocalAccountAdministration>();

        builder.Services.AddEnterpriseWorkflowModules(moduleCatalog, handlers => { foreach (var configure in _handlerConfiguration) configure(handlers); });
        var smtp = builder.Configuration.GetSection("Notifications:Smtp").Get<WorkflowSmtpOptions>() ?? new();
        smtp.Validate();
        builder.Services.AddSingleton(smtp);
        builder.Services.TryAddSingleton<IExternalEffectTransport>(services => new WorkflowNotificationTransport(
            services.GetRequiredService<IWorkflowStore>(), services.GetRequiredService<IWorkflowAuthorizationService>(),
            services.GetRequiredService<ISecurityProfileStore>(), services.GetRequiredService<ILocalAccountStore>(),
            services.GetRequiredService<IReadOnlyDictionary<string, IIdentityDirectory>>(), smtp, new SmtpWorkflowMailSender(smtp), new KernelExternalEffectTransport()) {
                        CulturePreferences = services.GetService<IUserCulturePreferenceStore>(),
                        DefaultCulture = localization.DefaultCulture,
                        FallbackCulture = localization.FallbackCulture,
                        SupportedCultures = localization.SupportedCultures.ToHashSet(StringComparer.OrdinalIgnoreCase),
                    });
        builder.Services.AddEnterpriseWorkflowRuntime<KernelExternalEffectTransport>(options => { options.NotifyHumanTaskPools = smtp.Enabled; builder.Configuration.GetSection("Workflow:Serialization").Bind(options.Serialization); foreach (var configure in _runtimeConfiguration) configure(options); });
        builder.Services.AddEnterpriseWorkflowPresentation(options =>
        {
            options.InstallationId = builder.Configuration["Installation:Id"] ?? "enterprise-workflow";
            options.MediaRoot = builder.Configuration["Media:Root"] ?? Path.Combine(AppContext.BaseDirectory, "media");
            foreach (var configure in _presentationConfiguration) configure(options);
        });
        if (_web)
        {
            builder.Services.AddEnterpriseWorkflowLocalization(localization);
            builder.Services.AddEnterpriseWorkflowWeb();
        }


        ValidateSingleRegistration<IAuthenticationProvider>();
        ValidateSingleRegistration<ISecondFactorHandler>();
        var host = _builder is WebApplicationBuilder web ? (IHost)web.Build() : ((HostApplicationBuilder)_builder).Build();
        try
        {
            var moduleLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("EnterpriseWorkflow.Modules");
            foreach (var diagnostic in moduleDiagnostics)
                if (moduleLogger.IsEnabled(LogLevel.Information))
                    WorkflowModuleSourceLog.Report(moduleLogger, diagnostic.Code, diagnostic.Message, diagnostic.Path);
            // Resolve capability registries now, without a temporary service provider.
            _ = host.Services.GetRequiredService<IReadOnlyDictionary<string, IIdentityDirectory>>();
            EnsureUnique(host.Services.GetServices<IIdentityStatusProvider>().Select(item => item.ProviderId));
            EnsureUnique(host.Services.GetServices<IIdentityAdmissionProvider>().Select(item => item.ProviderId));
            return new(host, _web);
        }
        catch { host.Dispose(); throw; }
    }

    private static Dictionary<string, IIdentityDirectory> CreateDirectories(IServiceProvider services,
        Dictionary<string, IIdentityDirectory> defaults)
    {
        var result = defaults.Values.Concat(services.GetServices<IIdentityDirectory>()).ToDictionary(item => item.ProviderId, StringComparer.Ordinal);
        var resolvers = services.GetServices<IIdentityResolver>().ToDictionary(item => item.ProviderId, StringComparer.Ordinal);
        var searches = services.GetServices<IIdentitySearchProvider>().ToDictionary(item => item.ProviderId, StringComparer.Ordinal);
        var statuses = services.GetServices<IIdentityStatusProvider>().ToDictionary(item => item.ProviderId, StringComparer.Ordinal);
        foreach (var id in resolvers.Keys.Concat(searches.Keys).Concat(statuses.Keys).Distinct(StringComparer.Ordinal))
        {
            if (result.ContainsKey(id)) continue;
            resolvers.TryGetValue(id, out var resolver); searches.TryGetValue(id, out var search); statuses.TryGetValue(id, out var status);
            result.Add(id, new CapabilityIdentityDirectory(id, resolver, search, status));
        }
        return result;
    }

    private void ValidateSingleRegistration<T>()
    {
        if (Services.Count(item => item.ServiceType == typeof(T)) > 1)
            throw new InvalidOperationException($"Only one {typeof(T).Name} may be registered.");
    }
    private static void EnsureUnique(IEnumerable<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) if (!seen.Add(id)) throw new InvalidOperationException("Duplicate identity capability provider identifier.");
    }
    private static void ConfigureDefaultAuthentication(IConfiguration configuration, IServiceCollection services,
        LocalAuthenticationProvider localAuthentication, Dictionary<string, IIdentityDirectory> configuredDirectories)
    {
        if (services.Any(item => item.ServiceType == typeof(IAuthenticationProvider))) return;
        var builder = new ConfigurationView(configuration);
        IAuthenticationProvider selectedAuthentication = localAuthentication;
var ldapSection = builder.Configuration.GetSection("Security:Ldap");
if (ldapSection.GetValue<bool>("Enabled"))
{
    var ldapOptions = ldapSection.Get<LdapDirectoryOptions>()
        ?? throw new InvalidOperationException("Security:Ldap configuration is incomplete.");
    ldapOptions.Validate();
    var serviceUserName = ldapSection["ServiceUserName"]
        ?? throw new InvalidOperationException("Security:Ldap:ServiceUserName is required when LDAP is enabled.");
    var servicePassword = ldapSection["ServicePassword"]
        ?? throw new InvalidOperationException("Security:Ldap:ServicePassword is required when LDAP is enabled.");
    var ldap = new LdapDirectoryProvider(ldapOptions,
        new KernelLdapCredentialProvider(serviceUserName, servicePassword));
    configuredDirectories.Add(ldap.ProviderId, ldap);
    var authenticationProvider = builder.Configuration["Security:AuthenticationProvider"] ?? SecurityProviderIds.Local;
    if (string.Equals(authenticationProvider, ldap.ProviderId, StringComparison.Ordinal))
        selectedAuthentication = ldap;
    else if (!string.Equals(authenticationProvider, SecurityProviderIds.Local, StringComparison.Ordinal))
        throw new InvalidOperationException(
            $"Security:AuthenticationProvider '{authenticationProvider}' is not configured.");
}
else if (!string.Equals(builder.Configuration["Security:AuthenticationProvider"] ?? SecurityProviderIds.Local,
             SecurityProviderIds.Local, StringComparison.Ordinal))
{
    throw new InvalidOperationException("A non-local authentication provider requires Security:Ldap:Enabled=true.");
}

        services.AddSingleton<IAuthenticationProvider>(selectedAuthentication);
    }
    private sealed record ConfigurationView(IConfiguration Configuration);
}

public sealed class EnterpriseWorkflowHost : IDisposable, IAsyncDisposable
{
    private readonly IHost _host;
    private readonly bool _web;
    private bool _mapped;
    internal EnterpriseWorkflowHost(IHost host, bool web) { _host = host; _web = web; }
    public IServiceProvider Services => _host.Services;
    public WebApplication WebApplication => _host as WebApplication ?? throw new InvalidOperationException("This is a non-web host.");

    public EnterpriseWorkflowHost MapEnterpriseWorkflow()
    {
        if (!_web) throw new InvalidOperationException("Web presentation was not registered.");
        if (_mapped) throw new InvalidOperationException("Enterprise Workflow endpoints have already been mapped.");
        _mapped = true;
        var app = WebApplication;
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        var rclWebRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "EnterpriseWorkflow.Web", "wwwroot"));
        if (Directory.Exists(rclWebRoot)) app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(rclWebRoot),
            RequestPath = "/_content/EnterpriseWorkflow.Web",
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapEnterpriseWorkflowModuleResources();
        app.MapEnterpriseWorkflowWeb();
        return this;
    }
    public Task RunAsync(CancellationToken cancellationToken = default) => _host.RunAsync(cancellationToken);
    public void Dispose() => _host.Dispose();
    public ValueTask DisposeAsync() => _host is IAsyncDisposable disposable ? disposable.DisposeAsync() : DisposeSync();
    private ValueTask DisposeSync() { _host.Dispose(); return ValueTask.CompletedTask; }
}

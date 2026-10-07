using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Oracle;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence;
using EnterpriseWorkflow.Security.Persistence.Oracle;
using EnterpriseWorkflow.Security.Persistence.Sqlite;
using EnterpriseWorkflow.Web;
using Microsoft.Data.Sqlite;
using System.Net;

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environments.Production;
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
{
    Args = [],
    ApplicationName = typeof(Program).Assembly.GetName().Name,
    ContentRootPath = AppContext.BaseDirectory,
    EnvironmentName = environmentName,
});
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddEnvironmentVariables("DOTNET_")
    .AddEnvironmentVariables("ASPNETCORE_");
builder.Logging.AddSimpleConsole();
builder.WebHost.UseKestrel();
Microsoft.AspNetCore.Hosting.StaticWebAssets.StaticWebAssetsLoader.UseStaticWebAssets(
    builder.Environment,
    builder.Configuration);
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var migrationMode = Enum.Parse<DatabaseMigrationMode>(
    builder.Configuration["Database:MigrationMode"] ?? nameof(DatabaseMigrationMode.Validate), ignoreCase: true);
var database = CreateDatabaseRuntime(provider, builder.Configuration);
var migrateCommand = args is ["migrate"];
await PrepareSchemasAsync(database, migrateCommand ? DatabaseMigrationMode.Apply : migrationMode);
if (migrateCommand) return;

var policy = builder.Configuration.GetSection("Security:Policy").Get<SecurityPolicyOptions>() ?? new();
policy.Validate();
var timeProvider = TimeProvider.System;
var securityStore = database.CreateSecurityStore(timeProvider);
var localAuthentication = new LocalAuthenticationProvider(securityStore, policy, timeProvider);
if (args is ["bootstrap", var userName, var password])
{
    var result = await new LocalSecurityProvisioner(securityStore, localAuthentication)
        .BootstrapAsync(userName, password, CancellationToken.None);
    Console.WriteLine(result.Outcome is ProviderOutcome.Succeeded
        ? "Administrateur local initialisé."
        : $"Échec: {result.ErrorCode}");
    return;
}

var moduleDirectories = builder.Configuration.GetSection("Modules:Directories").Get<string[]>() ?? [];
var moduleCatalog = WorkflowModuleCatalog.Load(moduleDirectories);
await WorkflowModuleKernel.ReconcileAsync(moduleCatalog, database.WorkflowMaintenanceStore);

var configuredDirectories = new Dictionary<string, IIdentityDirectory>(StringComparer.Ordinal);
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
builder.Services.AddSingleton<IPresentationSettingsStore>(securityStore);
builder.Services.AddSingleton(directories);
builder.Services.AddSingleton(localAuthentication);
builder.Services.AddSingleton<IAuthenticationProvider>(selectedAuthentication);
builder.Services.AddSingleton<SecuritySessionManager>();
builder.Services.AddSingleton<IAuthorizationProvider>(services =>
    new InternalProfileAuthorizationProvider(services.GetRequiredService<ISecurityProfileStore>(), directories));
builder.Services.AddSingleton<IWorkflowAuthorizationService>(services =>
    new InternalWorkflowAuthorizationService(services.GetRequiredService<IWorkflowAccessStore>(), directories));
builder.Services.AddSingleton<IWorkflowActionCatalog>(services =>
{
    var presentation = services.GetRequiredService<IWorkflowPresentationCatalog>();
    var descriptors = presentation.Processes.SelectMany(process =>
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

builder.Services.AddEnterpriseWorkflowModules(moduleCatalog);
builder.Services.AddEnterpriseWorkflowRuntime<KernelExternalEffectTransport>();
builder.Services.AddEnterpriseWorkflowPresentation(options =>
    options.InstallationId = builder.Configuration["Installation:Id"] ?? "enterprise-workflow");
builder.Services.AddEnterpriseWorkflowWeb();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
var rclWebRoot = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "EnterpriseWorkflow.Web", "wwwroot"));
if (Directory.Exists(rclWebRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(rclWebRoot),
        RequestPath = "/_content/EnterpriseWorkflow.Web",
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapEnterpriseWorkflowModuleResources();
app.MapEnterpriseWorkflowWeb();
await app.RunAsync();

static DatabaseRuntime CreateDatabaseRuntime(string provider, IConfiguration configuration)
{
    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "enterprise-workflow.db");
        var connectionString = configuration.GetConnectionString("EnterpriseWorkflow") ??
            new SqliteConnectionStringBuilder { DataSource = defaultPath }.ToString();
        var workflowDatabase = new SqliteWorkflowDatabase(connectionString);
        var securityDatabase = new SqliteSecurityDatabase(connectionString);
        var workflowStore = new SqliteWorkflowStore(workflowDatabase);
        return new(workflowStore, workflowStore, securityDatabase.CreateStore,
            workflowDatabase.GetPendingMigrationsAsync, workflowDatabase.MigrateAsync,
            securityDatabase.GetPendingMigrationsAsync, securityDatabase.MigrateAsync);
    }
    if (string.Equals(provider, "Oracle", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = configuration.GetConnectionString("EnterpriseWorkflow") ??
            throw new InvalidOperationException("ConnectionStrings:EnterpriseWorkflow is required for Oracle.");
        var workflowDatabase = new OracleWorkflowDatabase(connectionString);
        var securityDatabase = new OracleSecurityDatabase(connectionString);
        var workflowStore = new OracleWorkflowStore(workflowDatabase);
        return new(workflowStore, workflowStore, securityDatabase.CreateStore,
            workflowDatabase.GetPendingMigrationsAsync, workflowDatabase.MigrateAsync,
            securityDatabase.GetPendingMigrationsAsync, securityDatabase.MigrateAsync);
    }
    throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
}

static async Task PrepareSchemasAsync(DatabaseRuntime database, DatabaseMigrationMode mode)
{
    if (mode is DatabaseMigrationMode.Apply)
    {
        await database.MigrateWorkflow(CancellationToken.None);
        await database.MigrateSecurity(CancellationToken.None);
        return;
    }
    var workflow = await database.PendingWorkflow(CancellationToken.None);
    var security = await database.PendingSecurity(CancellationToken.None);
    if (workflow.Count > 0 || security.Count > 0)
        throw new InvalidOperationException(
            $"Database schema is not ready. Pending workflow migrations: {string.Join(", ", workflow)}; pending security migrations: {string.Join(", ", security)}. Run the Kernel 'migrate' command or select Apply explicitly.");
}

internal enum DatabaseMigrationMode { Validate, Apply }

internal sealed record DatabaseRuntime(
    IWorkflowStore WorkflowStore,
    IWorkflowMaintenanceStore WorkflowMaintenanceStore,
    Func<TimeProvider, RelationalSecurityStore> CreateSecurityStore,
    Func<CancellationToken, Task<IReadOnlyList<string>>> PendingWorkflow,
    Func<CancellationToken, Task> MigrateWorkflow,
    Func<CancellationToken, Task<IReadOnlyList<string>>> PendingSecurity,
    Func<CancellationToken, Task> MigrateSecurity);

internal sealed class KernelExternalEffectTransport : IExternalEffectTransport
{
    public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(
        OutboxLease message,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ExternalEffectDeliveryResult.PermanentFailure("kernel.transport-not-configured"));
}

internal sealed class KernelLdapCredentialProvider(string userName, string password) : ILdapServiceCredentialProvider
{
    public ValueTask<NetworkCredential> GetCredentialAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new NetworkCredential(userName, password));
}

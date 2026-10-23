using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

if (args.Length < 5)
{
    Console.WriteLine("Usage: dotnet run -- <artifact-directory> <process-id> <revision> <submission-json> <permission-id> [permission-id...]");
    return 1;
}
var catalog = WorkflowModuleCatalog.Load([Path.GetFullPath(args[0])]);
var databasePath = Path.GetFullPath("embedded-workflow.db");
var connection = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
var database = new SqliteWorkflowDatabase(connection);
var security = new SqliteSecurityDatabase(connection);
await database.MigrateAsync(CancellationToken.None);
await security.MigrateAsync(CancellationToken.None);
var store = new SqliteWorkflowStore(database);
var securityStore = security.CreateStore(TimeProvider.System);
await WorkflowModuleKernel.ReconcileAsync(catalog, store);
var directories = new Dictionary<string, IIdentityDirectory>();
var services = new ServiceCollection();
services.AddSingleton<IWorkflowStore>(store);
services.AddSingleton<ISecurityProfileStore>(securityStore);
services.AddSingleton<ILocalAccountStore>(securityStore);
services.AddSingleton<IWorkflowAccessStore>(securityStore);
services.AddSingleton<IReadOnlyDictionary<string, IIdentityDirectory>>(directories);
services.AddSingleton<IAuthorizationProvider>(new InternalProfileAuthorizationProvider(securityStore, directories));
services.AddEnterpriseWorkflowModules(catalog);
services.AddSingleton<IWorkflowAuthorizationService>(provider => new ModulePermissionAuthorizationService(
    provider.GetRequiredService<IWorkflowPresentationCatalog>(), provider.GetRequiredService<IAuthorizationProvider>(),
    new InternalWorkflowAuthorizationService(securityStore, directories)));
services.AddEnterpriseWorkflowRuntime<UnconfiguredTransport>();
services.AddEnterpriseWorkflowPresentation(options => options.InstallationId = "sample.embedded");
await using var provider = services.BuildServiceProvider();
var presentation = provider.GetRequiredService<IWorkflowPresentationCatalog>();
var requested = args[4..].ToHashSet(StringComparer.Ordinal);
if (requested.Any(permission => !presentation.Permissions.Any(known => known.Id.Value == permission)))
    throw new ArgumentException("Unknown module permission.");
// Demonstration identity supplied by the embedding application; no permissive authorization stub.
var actor = new IdentityReference("embedded", "requester");
var profile = new SecurityProfile("embedded-requester", "Embedded requester", requested.Select(value => new PermissionId(value)).ToHashSet(), false);
var saved = await securityStore.UpsertProfileAsync(profile, actor, CancellationToken.None);
if (saved.Outcome != ProviderOutcome.Succeeded) throw new InvalidOperationException(saved.ErrorCode);
var assigned = await securityStore.AssignIdentityAsync(actor, profile.Id, actor, CancellationToken.None);
if (assigned.Outcome != ProviderOutcome.Succeeded) throw new InvalidOperationException(assigned.ErrorCode);
var portal = provider.GetRequiredService<IWorkflowPortalService>();
var result = await portal.StartAsync(actor, new(args[1], int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture),
    args[3], new("embedded-" + Guid.NewGuid().ToString("N")), new Dictionary<EnterpriseWorkflow.Abstractions.TechnicalId, IdentityReference>()), CancellationToken.None);
if (!result.Succeeded) throw new InvalidOperationException(result.ErrorCode ?? string.Join(", ", result.Errors));
var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
while (await pump.ExecuteNextAsync(new("sample.embedded-worker"), CancellationToken.None)) { }
Console.WriteLine($"Instance: {result.InstanceId}; SQLite: {databasePath}");
return 0;

internal sealed class UnconfiguredTransport : IExternalEffectTransport
{
    public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ExternalEffectDeliveryResult.PermanentFailure("embedded.transport-not-configured"));
}

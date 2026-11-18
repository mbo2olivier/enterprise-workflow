using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Persistence.Oracle;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence;
using EnterpriseWorkflow.Security.Persistence.Sqlite;
using EnterpriseWorkflow.Security.Persistence.Oracle;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace EnterpriseWorkflow.Hosting;

internal static class WorkflowDatabaseFactory
{
internal static WorkflowDatabaseRuntime CreateDatabaseRuntime(string provider, IConfiguration configuration)
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

internal static async Task PrepareSchemasAsync(WorkflowDatabaseRuntime database, DatabaseMigrationMode mode)
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

}

public enum DatabaseMigrationMode { Validate, Apply }

public sealed record WorkflowDatabaseRuntime(
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

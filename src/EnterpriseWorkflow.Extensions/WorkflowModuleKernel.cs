using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Extensions;

/// <summary>Startup gate that makes the loaded module catalog durable before the Host becomes ready.</summary>
public static class WorkflowModuleKernel
{
    public static async ValueTask<ModuleArtifactReconciliationResult> ReconcileAsync(
        WorkflowModuleCatalog catalog,
        IWorkflowMaintenanceStore store,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(store);
        var configured = catalog.Modules.Select(module => new ModuleArtifactReference(
            module.Manifest.Id, module.Manifest.Version, module.ArtifactSha256)).ToArray();
        var result = await store.ReconcileModuleArtifactsAsync(
            new ReconcileModuleArtifactsCommand(configured), cancellationToken).ConfigureAwait(false);
        if (result.Outcome is StoreOutcome.Succeeded && result.Value is not null) return result.Value;
        throw new WorkflowModuleReadinessException(result.ErrorCode ?? "EW7022_MODULE_RECONCILIATION_FAILED",
            "The configured module catalog could not be reconciled with durable references.");
    }
}

public sealed class WorkflowModuleReadinessException : InvalidOperationException
{
    public WorkflowModuleReadinessException(string code, string message)
        : base($"{code}: {message}") => Code = code;

    public string Code { get; }
}

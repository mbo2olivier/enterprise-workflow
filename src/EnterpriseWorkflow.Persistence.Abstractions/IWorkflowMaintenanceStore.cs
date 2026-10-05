namespace EnterpriseWorkflow.Persistence;

/// <summary>
/// Administrative persistence boundary used during Kernel startup and explicit state migration.
/// It is intentionally separate from the worker-facing <see cref="IWorkflowStore"/>.
/// </summary>
public interface IWorkflowMaintenanceStore
{
    /// <summary>
    /// Atomically reconciles the configured immutable module artifacts with the durable inventory.
    /// A persisted definition prevents removal of every exact artifact it references.
    /// </summary>
    ValueTask<StoreResult<ModuleArtifactReconciliationResult>> ReconcileModuleArtifactsAsync(
        ReconcileModuleArtifactsCommand command,
        CancellationToken cancellationToken);

    /// <summary>Reads the current state and immutable definition required to select one migration.</summary>
    ValueTask<StoreResult<StateMigrationSnapshot>> ReadStateMigrationSnapshotAsync(
        WorkflowInstanceId instanceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically commits one explicit state-schema transition if the instance remains inactive
    /// and its revision and source schema still match.
    /// </summary>
    ValueTask<StoreResult<StateMigrationResult>> CommitStateMigrationAsync(
        CommitStateMigrationCommand command,
        CancellationToken cancellationToken);
}

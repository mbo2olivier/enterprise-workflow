namespace EnterpriseWorkflow.Persistence;

/// <summary>
/// Atomic persistence boundary. Implementations own their transactions and authoritative lease clock.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>Publishes an immutable definition or returns its identical existing record.</summary>
    ValueTask<StoreResult<DefinitionReference>> PublishDefinitionAsync(
        PublishDefinitionCommand command,
        CancellationToken cancellationToken);

    /// <summary>Creates an instance, first activation/work, receipt and audit atomically.</summary>
    ValueTask<StoreResult<StartInstanceResult>> StartInstanceAsync(
        StartInstanceCommand command,
        CancellationToken cancellationToken);

    /// <summary>Claims at most one due work item and advances its fencing generation atomically.</summary>
    ValueTask<StoreResult<WorkLease>> ClaimDueWorkAsync(
        ClaimDueWorkCommand command,
        CancellationToken cancellationToken);

    /// <summary>Renews a non-expired current lease using the same provider clock as claim and commit.</summary>
    ValueTask<StoreResult<WorkLease>> RenewLeaseAsync(
        RenewLeaseCommand command,
        CancellationToken cancellationToken);

    /// <summary>Commits state, next work or terminal status only for the current unexpired lease.</summary>
    ValueTask<StoreResult<CommitNodeResult>> CommitNodeResultAsync(
        CommitNodeResultCommand command,
        CancellationToken cancellationToken);

    /// <summary>Cancels a non-terminal instance and invalidates all outstanding work atomically.</summary>
    ValueTask<StoreResult<CancelInstanceResult>> CancelInstanceAsync(
        CancelInstanceCommand command,
        CancellationToken cancellationToken);
}


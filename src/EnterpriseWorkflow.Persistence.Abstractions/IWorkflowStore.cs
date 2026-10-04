using EnterpriseWorkflow.Abstractions;

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
    ValueTask<StoreResult<ClaimedWork>> ClaimDueWorkAsync(
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

    /// <summary>Reads one durable human task with its immutable definition and separation history.</summary>
    ValueTask<StoreResult<HumanTaskSnapshot>> GetHumanTaskAsync(
        HumanTaskId taskId,
        CancellationToken cancellationToken);

    /// <summary>Assigns one awaiting designated task atomically.</summary>
    ValueTask<StoreResult<HumanTaskMutationResult>> AssignHumanTaskAsync(
        AssignHumanTaskCommand command,
        CancellationToken cancellationToken);

    /// <summary>Claims one available pool task atomically.</summary>
    ValueTask<StoreResult<HumanTaskMutationResult>> ClaimHumanTaskAsync(
        ClaimHumanTaskCommand command,
        CancellationToken cancellationToken);

    /// <summary>Releases one pool claim atomically; only its current owner may do so.</summary>
    ValueTask<StoreResult<HumanTaskMutationResult>> ReleaseHumanTaskAsync(
        ReleaseHumanTaskCommand command,
        CancellationToken cancellationToken);

    /// <summary>Reads an existing completion receipt without re-evaluating current grants.</summary>
    ValueTask<StoreResult<HumanTaskCompletionReceipt>> GetHumanTaskCompletionReceiptAsync(
        HumanTaskId taskId,
        TechnicalId idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Completes a task, records its receipt and creates the next work atomically.</summary>
    ValueTask<StoreResult<CompleteHumanTaskResult>> CompleteHumanTaskAsync(
        CompleteHumanTaskCommand command,
        CancellationToken cancellationToken);

    /// <summary>Reads a stable bounded page of open tasks for authorization filtering.</summary>
    ValueTask<StoreResult<OpenHumanTaskPage>> ReadOpenHumanTasksAsync(
        ReadOpenHumanTasksCommand command,
        CancellationToken cancellationToken);

    /// <summary>Fires one due timer once, using the authoritative store clock.</summary>
    ValueTask<StoreResult<FireDueTimerResult>> FireDueTimerAsync(
        FireDueTimerCommand command,
        CancellationToken cancellationToken);

    /// <summary>Selects and fires at most one due timer atomically for restart-safe polling.</summary>
    ValueTask<StoreResult<FireNextDueTimerResult>> FireNextDueTimerAsync(
        CancellationToken cancellationToken);

    /// <summary>Cancels a non-terminal instance and invalidates all outstanding work atomically.</summary>
    ValueTask<StoreResult<CancelInstanceResult>> CancelInstanceAsync(
        CancelInstanceCommand command,
        CancellationToken cancellationToken);

    /// <summary>Claims at most one due outbox message using a fenced delivery lease.</summary>
    ValueTask<StoreResult<OutboxLease>> ClaimDueOutboxAsync(
        ClaimDueOutboxCommand command,
        CancellationToken cancellationToken);

    /// <summary>Commits delivery or schedules a retry for the current outbox lease.</summary>
    ValueTask<StoreResult<CommitOutboxResult>> CommitOutboxAsync(
        CommitOutboxCommand command,
        CancellationToken cancellationToken);
}

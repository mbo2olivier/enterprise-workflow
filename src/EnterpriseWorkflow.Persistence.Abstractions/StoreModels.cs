using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Persistence;

/// <summary>Durable workflow instance identifier.</summary>
public readonly record struct WorkflowInstanceId(Guid Value);

/// <summary>Durable work item identifier.</summary>
public readonly record struct WorkItemId(Guid Value);

/// <summary>Stable node activation identifier, retained across retries.</summary>
public readonly record struct NodeActivationId(Guid Value);

/// <summary>Durable human-task identifier.</summary>
public readonly record struct HumanTaskId(Guid Value);

/// <summary>Durable timer identifier.</summary>
public readonly record struct WorkflowTimerId(Guid Value);

/// <summary>Distinct fencing token for one lease ownership generation.</summary>
public readonly record struct LeaseToken(Guid Value);

/// <summary>Stable authenticated actor identity. Empty subject identifiers are not portable to Oracle.</summary>
public sealed record ActorIdentity
{
    /// <summary>Creates a provider-qualified identity.</summary>
    public ActorIdentity(TechnicalId providerId, string subjectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ProviderId = providerId;
        SubjectId = subjectId;
    }

    /// <summary>Gets the identity provider identifier.</summary>
    public TechnicalId ProviderId { get; }

    /// <summary>Gets the non-empty, provider-stable subject identifier.</summary>
    public string SubjectId { get; }
}

/// <summary>Scope of a start-command idempotency key.</summary>
public sealed record StartCommandScope(
    TechnicalId InstallationId,
    TechnicalId CommandTypeId,
    ActorIdentity Actor);

/// <summary>Reference to an immutable published definition.</summary>
public sealed record DefinitionReference(TechnicalId DefinitionId, int Version, string Sha256);

/// <summary>Exact immutable module artifact installed in one Enterprise Workflow deployment.</summary>
public sealed record ModuleArtifactReference(TechnicalId Id, string Version, string Sha256);

/// <summary>Configured artifact set reconciled once during Kernel startup.</summary>
public sealed record ReconcileModuleArtifactsCommand(IReadOnlyList<ModuleArtifactReference> ConfiguredArtifacts);

/// <summary>Durable artifact inventory after a successful startup reconciliation.</summary>
public sealed record ModuleArtifactReconciliationResult(IReadOnlyList<ModuleArtifactReference> InstalledArtifacts);

/// <summary>State of a workflow instance.</summary>
public enum WorkflowInstanceStatus
{
    /// <summary>The start transaction committed but no work has been claimed.</summary>
    Created,

    /// <summary>The instance is executing.</summary>
    Running,

    /// <summary>The instance is durably waiting.</summary>
    Waiting,

    /// <summary>The workflow reached an end node.</summary>
    Completed,

    /// <summary>A permanent failure or exhausted retry ended execution.</summary>
    Failed,

    /// <summary>A cancellation transaction won.</summary>
    Cancelled,
}

/// <summary>Consistent state snapshot supplied to one explicit versioned migrator.</summary>
public sealed record StateMigrationSnapshot(
    WorkflowInstanceId InstanceId,
    DefinitionReference Definition,
    IReadOnlyList<ModuleArtifactReference> Artifacts,
    WorkflowInstanceStatus Status,
    long Revision,
    WorkflowState State);

/// <summary>Atomic compare-and-swap command for one state schema transition.</summary>
public sealed record CommitStateMigrationCommand(
    WorkflowInstanceId InstanceId,
    long ExpectedRevision,
    int SourceSchemaVersion,
    WorkflowState ReplacementState,
    ModuleArtifactReference MigratorArtifact,
    ActorIdentity Actor);

/// <summary>Committed explicit state migration.</summary>
public sealed record StateMigrationResult(long Revision, int SchemaVersion);

/// <summary>State of a durable work item.</summary>
public enum WorkItemStatus
{
    /// <summary>The work can be claimed when due.</summary>
    Ready,

    /// <summary>The work has one current fenced owner.</summary>
    Leased,

    /// <summary>The work committed its result.</summary>
    Done,

    /// <summary>The work was invalidated.</summary>
    Cancelled,
}

/// <summary>State of one logical node activation.</summary>
public enum NodeExecutionStatus
{
    /// <summary>The activation is waiting to execute.</summary>
    Pending,

    /// <summary>A worker owns the current attempt.</summary>
    Running,

    /// <summary>The activation committed successfully.</summary>
    Succeeded,

    /// <summary>The activation is durably waiting.</summary>
    Waiting,

    /// <summary>The activation ended permanently.</summary>
    Failed,

    /// <summary>The activation was invalidated.</summary>
    Cancelled,
}

/// <summary>Current fenced lease returned by the store clock.</summary>
public sealed record WorkLease(
    WorkItemId WorkItemId,
    NodeActivationId ActivationId,
    WorkflowInstanceId InstanceId,
    TechnicalId NodeId,
    TechnicalId OwnerId,
    long Generation,
    LeaseToken Token,
    DateTimeOffset StoreUtcNow,
    DateTimeOffset ExpiresAtUtc,
    long InstanceRevision);

/// <summary>Atomic execution snapshot returned by a successful work claim.</summary>
public sealed record ClaimedWork(
    WorkLease Lease,
    WorkflowDefinition Definition,
    WorkflowState State,
    int Attempt);

/// <summary>Description of a next logical work item created by a node commit.</summary>
public sealed record NextWork(TechnicalId NodeId, DateTimeOffset DueAtUtc);

/// <summary>Kind of atomic result committed for a leased node.</summary>
public enum NodeCommitKind
{
    /// <summary>Close the current work and create the next work.</summary>
    Continue,

    /// <summary>Return the same logical activation to pending with a future due date.</summary>
    Retry,

    /// <summary>Complete the instance.</summary>
    Complete,

    /// <summary>Fail the instance permanently.</summary>
    Fail,

    /// <summary>Close worker execution and persist a durable human wait.</summary>
    WaitHumanTask,

    /// <summary>Close worker execution and persist a durable timer wait.</summary>
    WaitTimer,
}

/// <summary>State of a durable human task.</summary>
public enum HumanTaskStatus
{
    AwaitingAssignment,
    Assigned,
    Available,
    Claimed,
    Completed,
    Cancelled,
}

/// <summary>State of a durable timer.</summary>
public enum WorkflowTimerStatus
{
    Waiting,
    Fired,
    Cancelled,
}

/// <summary>An identity prepared for a designated human-task node.</summary>
public sealed record DesignatedTaskAssignment(TechnicalId NodeId, ActorIdentity Assignee);

/// <summary>Creates a human wait from the currently leased activation.</summary>
public sealed record HumanTaskWait(HumanTaskAssignmentMode AssignmentMode);

/// <summary>Creates a fixed-delay timer from the currently leased activation.</summary>
public sealed record TimerWait(TimeSpan Delay, TechnicalId NextNodeId);

/// <summary>Atomic definition publication command.</summary>
public sealed record PublishDefinitionCommand(WorkflowDefinition Definition);

/// <summary>Atomic, idempotent instance start command.</summary>
public sealed record StartInstanceCommand(
    StartCommandScope Scope,
    TechnicalId IdempotencyKey,
    string RequestSha256,
    DefinitionReference Definition,
    WorkflowState InitialState,
    string? BusinessKey,
    string? CorrelationId,
    DateTimeOffset RequestedAtUtc,
    IReadOnlyList<DesignatedTaskAssignment>? DesignatedAssignments = null);

/// <summary>Result persisted for a start receipt.</summary>
public sealed record StartInstanceResult(WorkflowInstanceId InstanceId, bool WasCreated, long Revision);

/// <summary>Bounded initiator-owned instance list without exposing workflow state.</summary>
public sealed record ReadWorkflowInstancesCommand(ActorIdentity Initiator, int Limit);

/// <summary>Safe workflow instance summary for application read models.</summary>
public sealed record WorkflowInstanceSummary(
    WorkflowInstanceId InstanceId,
    TechnicalId DefinitionId,
    int DefinitionVersion,
    WorkflowInstanceStatus Status,
    long Revision,
    string? BusinessKey,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Claims at most one due work item using the store clock.</summary>
public sealed record ClaimDueWorkCommand(TechnicalId OwnerId, TimeSpan LeaseDuration);

/// <summary>Renews a still-current lease using the store clock.</summary>
public sealed record RenewLeaseCommand(
    WorkItemId WorkItemId,
    LeaseToken Token,
    long Generation,
    TimeSpan LeaseDuration);

/// <summary>Commits one leased node result conditionally and atomically.</summary>
public sealed record CommitNodeResultCommand(
    WorkItemId WorkItemId,
    LeaseToken Token,
    long Generation,
    long ExpectedInstanceRevision,
    NodeCommitKind Kind,
    WorkflowState? ReplacementState,
    NextWork? NextWork,
    string? ErrorCode,
    IReadOnlyList<OutboxWrite>? Outbox = null,
    HumanTaskWait? HumanTask = null,
    TimerWait? Timer = null);

/// <summary>Result of a successful node commit.</summary>
public sealed record CommitNodeResult(
    WorkflowInstanceStatus InstanceStatus,
    long Revision,
    HumanTaskId? HumanTaskId = null,
    WorkflowTimerId? TimerId = null);

/// <summary>Durable human-task snapshot used by application services.</summary>
public sealed record HumanTaskSnapshot(
    HumanTaskId TaskId,
    NodeActivationId ActivationId,
    WorkflowInstanceId InstanceId,
    TechnicalId NodeId,
    HumanTaskStatus Status,
    HumanTaskAssignmentMode AssignmentMode,
    ActorIdentity? Assignee,
    long Revision,
    long InstanceRevision,
    ActorIdentity Initiator,
    WorkflowDefinition Definition,
    WorkflowState State,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<HumanTaskCompletionActor> CompletionHistory);

/// <summary>Actor and action of a previously completed task in the same instance.</summary>
public sealed record HumanTaskCompletionActor(TechnicalId NodeId, TechnicalId ActionId, ActorIdentity Actor);

/// <summary>Atomic assignment of one awaiting designated task.</summary>
public sealed record AssignHumanTaskCommand(
    HumanTaskId TaskId,
    long ExpectedRevision,
    ActorIdentity Assignee,
    ActorIdentity AssignedBy);

/// <summary>Atomic claim of one available pool task.</summary>
public sealed record ClaimHumanTaskCommand(HumanTaskId TaskId, long ExpectedRevision, ActorIdentity Actor);

/// <summary>Atomic release of a claim by its owner.</summary>
public sealed record ReleaseHumanTaskCommand(HumanTaskId TaskId, long ExpectedRevision, ActorIdentity Actor);

/// <summary>Result of an assignment, claim or release transition.</summary>
public sealed record HumanTaskMutationResult(HumanTaskStatus Status, long Revision, long InstanceRevision);

/// <summary>Previously committed idempotent completion result.</summary>
public sealed record HumanTaskCompletionReceipt(
    HumanTaskId TaskId,
    TechnicalId IdempotencyKey,
    ActorIdentity Actor,
    TechnicalId ActionId,
    string RequestSha256,
    long TaskRevision,
    long InstanceRevision);

/// <summary>Atomically completes a human task and creates the next work.</summary>
public sealed record CompleteHumanTaskCommand(
    HumanTaskId TaskId,
    long ExpectedTaskRevision,
    long ExpectedInstanceRevision,
    ActorIdentity Actor,
    TechnicalId ActionId,
    TechnicalId IdempotencyKey,
    string RequestSha256,
    WorkflowState? ReplacementState,
    NextWork NextWork,
    IReadOnlyList<OutboxWrite>? Outbox = null,
    IReadOnlyList<DesignatedTaskAssignment>? DesignatedAssignments = null);

/// <summary>Result of a committed or replayed task completion.</summary>
public sealed record CompleteHumanTaskResult(long TaskRevision, long InstanceRevision, bool WasCompleted);

/// <summary>Stable store cursor for bounded open-task scans.</summary>
public sealed record HumanTaskCursor(long CreatedAtUnixMilliseconds, HumanTaskId TaskId);

/// <summary>Reads one bounded page of open tasks after a stable cursor.</summary>
public sealed record ReadOpenHumanTasksCommand(HumanTaskCursor? After, int Limit);

/// <summary>Minimal candidate row returned before authorization filtering.</summary>
public sealed record OpenHumanTask(
    HumanTaskId TaskId,
    WorkflowInstanceId InstanceId,
    TechnicalId DefinitionId,
    int DefinitionVersion,
    TechnicalId NodeId,
    HumanTaskStatus Status,
    HumanTaskAssignmentMode AssignmentMode,
    ActorIdentity? Assignee,
    long Revision,
    DateTimeOffset CreatedAtUtc);

/// <summary>One stable page of open human tasks.</summary>
public sealed record OpenHumanTaskPage(IReadOnlyList<OpenHumanTask> Tasks, HumanTaskCursor? Next);

/// <summary>Atomically fires one due timer and creates its next work.</summary>
public sealed record FireDueTimerCommand(WorkflowTimerId TimerId, long ExpectedRevision);

/// <summary>Result of a timer firing.</summary>
public sealed record FireDueTimerResult(long TimerRevision, long InstanceRevision, TechnicalId NextNodeId);

/// <summary>Result of atomically selecting and firing the next due timer.</summary>
public sealed record FireNextDueTimerResult(
    WorkflowTimerId TimerId,
    WorkflowInstanceId InstanceId,
    long TimerRevision,
    long InstanceRevision,
    TechnicalId NextNodeId);

/// <summary>An external-effect intent persisted atomically with a successful node commit.</summary>
public sealed record OutboxWrite(
    TechnicalId OperationId,
    TechnicalId Destination,
    string ContentType,
    string PayloadJson);

/// <summary>Durable outbox message identifier.</summary>
public readonly record struct OutboxMessageId(Guid Value);

/// <summary>State of a durable outbox message.</summary>
public enum OutboxMessageStatus
{
    /// <summary>The message is ready for delivery.</summary>
    Ready,
    /// <summary>A dispatcher owns the current fenced delivery attempt.</summary>
    Leased,
    /// <summary>The dispatcher acknowledged delivery.</summary>
    Delivered,
    /// <summary>Delivery failed permanently or exhausted its configured attempts.</summary>
    Failed,
}

/// <summary>Claims one due outbox message.</summary>
public sealed record ClaimDueOutboxCommand(TechnicalId OwnerId, TimeSpan LeaseDuration);

/// <summary>Fenced outbox delivery lease with a stable receiver idempotency key.</summary>
public sealed record OutboxLease(
    OutboxMessageId MessageId,
    WorkflowInstanceId InstanceId,
    NodeActivationId ActivationId,
    TechnicalId OperationId,
    TechnicalId Destination,
    string ContentType,
    string PayloadJson,
    string IdempotencyKey,
    int Attempt,
    long Generation,
    LeaseToken Token,
    DateTimeOffset StoreUtcNow,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Kind of fenced outbox delivery result.</summary>
public enum OutboxCommitKind
{
    /// <summary>Delivery was acknowledged.</summary>
    Delivered,
    /// <summary>Delivery failed transiently and must be retried when due.</summary>
    Retry,
    /// <summary>Delivery is permanently abandoned and remains visible for operations.</summary>
    Fail,
}

/// <summary>Commits one current outbox delivery lease.</summary>
public sealed record CommitOutboxCommand(
    OutboxMessageId MessageId,
    LeaseToken Token,
    long Generation,
    OutboxCommitKind Kind,
    DateTimeOffset? RetryAtUtc,
    string? ErrorCode);

/// <summary>Result of an outbox delivery commit.</summary>
public sealed record CommitOutboxResult(OutboxMessageStatus Status, int Attempt);

/// <summary>Cancels one non-terminal instance at an expected revision.</summary>
public sealed record CancelInstanceCommand(
    WorkflowInstanceId InstanceId,
    long ExpectedRevision,
    ActorIdentity Actor,
    DateTimeOffset RequestedAtUtc);

/// <summary>Result of a committed cancellation.</summary>
public sealed record CancelInstanceResult(long Revision);

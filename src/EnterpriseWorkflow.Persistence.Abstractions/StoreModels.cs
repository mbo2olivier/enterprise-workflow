using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Persistence;

/// <summary>Durable workflow instance identifier.</summary>
public readonly record struct WorkflowInstanceId(Guid Value);

/// <summary>Durable work item identifier.</summary>
public readonly record struct WorkItemId(Guid Value);

/// <summary>Stable node activation identifier, retained across retries.</summary>
public readonly record struct NodeActivationId(Guid Value);

/// <summary>Distinct fencing token for one lease ownership generation.</summary>
public readonly record struct LeaseToken(Guid Value);

/// <summary>Stable authenticated actor identity.</summary>
public sealed record ActorIdentity(TechnicalId ProviderId, string SubjectId);

/// <summary>Scope of a start-command idempotency key.</summary>
public sealed record StartCommandScope(
    TechnicalId InstallationId,
    TechnicalId CommandTypeId,
    ActorIdentity Actor);

/// <summary>Reference to an immutable published definition.</summary>
public sealed record DefinitionReference(TechnicalId DefinitionId, int Version, string Sha256);

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
}

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
    DateTimeOffset RequestedAtUtc);

/// <summary>Result persisted for a start receipt.</summary>
public sealed record StartInstanceResult(WorkflowInstanceId InstanceId, bool WasCreated, long Revision);

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
    string? ErrorCode);

/// <summary>Result of a successful node commit.</summary>
public sealed record CommitNodeResult(WorkflowInstanceStatus InstanceStatus, long Revision);

/// <summary>Cancels one non-terminal instance at an expected revision.</summary>
public sealed record CancelInstanceCommand(
    WorkflowInstanceId InstanceId,
    long ExpectedRevision,
    ActorIdentity Actor,
    DateTimeOffset RequestedAtUtc);

/// <summary>Result of a committed cancellation.</summary>
public sealed record CancelInstanceResult(long Revision);

using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Persistence;

/// <summary>Candidate cursor only; application services must authorize every returned resource.</summary>
public sealed record WorkflowInstanceCursor(long CreatedAtUnixMilliseconds, string InstanceId);
public sealed record WorkflowInstanceSearch(
    int Limit = 50,
    WorkflowInstanceCursor? After = null,
    WorkflowInstanceStatus? Status = null,
    DateTimeOffset? CreatedFromUtc = null,
    DateTimeOffset? CreatedUntilUtc = null,
    string? ProcessId = null,
    int? ProcessVersion = null,
    WorkflowInstanceId? InstanceId = null,
    string? BusinessKey = null,
    ActorIdentity? Initiator = null,
    WorkflowInstanceStatus[]? Statuses = null, string? PublicReference = null);
public sealed record WorkflowInstanceCandidate(WorkflowInstanceSummary Summary, ActorIdentity Initiator);
public sealed record WorkflowInstanceReadSnapshot(
    WorkflowInstanceSummary Summary, WorkflowDefinition Definition, ActorIdentity Initiator, WorkflowState State, IReadOnlyList<WorkflowInstanceStep>? Steps = null, IReadOnlyList<WorkflowInstanceHistoryEntry>? RecentHistory = null);

public sealed record WorkflowInstanceStep(string NodeId, string Label, NodeExecutionStatus Status, int Attempt);
public sealed record WorkflowInstanceHistoryEntry(long Sequence, string EventType, DateTimeOffset OccurredAtUtc, ActorIdentity? Actor);

public sealed record WorkflowInstancePosition(string NodeId, NodeExecutionStatus Status);

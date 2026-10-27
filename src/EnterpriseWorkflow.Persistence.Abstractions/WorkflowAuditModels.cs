namespace EnterpriseWorkflow.Persistence;
public sealed record WorkflowAuditQuery(DateTimeOffset? FromUtc = null, DateTimeOffset? UntilUtc = null, string? ActorId = null,
    string? ActorProvider = null, string? Action = null, string? Outcome = null, string? ProcessId = null, string[]? ProcessIds = null,
    string? Stage = null, int Limit = 50, long? BeforeTime = null, long BeforeSequence = long.MaxValue, string? Target = null);
public sealed record WorkflowAuditRow(long Sequence, DateTimeOffset Date, ActorIdentity? Actor, string Action, string EventType,
    string Outcome, string? Module, string ProcessId, string ProcessTitle, string? StageId, string? StageLabel, string Reference, string? ActionLabel = null);

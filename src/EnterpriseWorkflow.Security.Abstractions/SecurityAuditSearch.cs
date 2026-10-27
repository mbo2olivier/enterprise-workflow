namespace EnterpriseWorkflow.Security;

public sealed record SecurityAuditCursor(long OccurredAtUnixMilliseconds, string Id);
public sealed record SecurityAuditQuery(DateTimeOffset? FromUtc = null, DateTimeOffset? UntilUtc = null,
    IdentityReference? Actor = null, string? Action = null, string? Target = null, string? Outcome = null,
    int Limit = 50, SecurityAuditCursor? After = null, string? Module = null, string? Process = null, string? Stage = null, string? ActorId = null, string? ActorProvider = null);
public sealed record SecurityAuditRecord(string Id, SecurityAuditEntry Entry, string? Module = null, string? Process = null, string? ProcessTitle = null, string? Stage = null, string? StageLabel = null, string? ActorDisplayName = null, string? ActionLabel = null);

namespace EnterpriseWorkflow.Security;

public static class SecurityProviderIds { public const string Local = "local"; }

public readonly record struct IdentityReference
{
    public IdentityReference(string providerId, string subjectId)
    {
        ProviderId = Required(providerId, nameof(providerId));
        SubjectId = Required(subjectId, nameof(subjectId));
    }

    public string ProviderId { get; }
    public string SubjectId { get; }

    private static string Required(string value, string parameter) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 256
            ? value
            : throw new ArgumentException("A stable identity component is required and limited to 256 characters.", parameter);
}

public readonly record struct PermissionId
{
    public PermissionId(string value) => Value =
        !string.IsNullOrWhiteSpace(value) && value.Length <= 160
            ? value
            : throw new ArgumentException("A permission identifier is required and limited to 160 characters.", nameof(value));

    public string Value { get; }
    public override string ToString() => Value;
}

public static class WorkflowPermissions
{
    public static readonly PermissionId ManageAccess = new("security.access.manage");
    public static readonly PermissionId ReadAudit = new("security.audit.read");
    public static readonly PermissionId SearchApprovalCandidates = new("workflow.approver.search");
    public static readonly PermissionId Approve = new("workflow.task.approve");
    public static readonly PermissionId Start = new("workflow.instance.start");
    public static readonly PermissionId ReadInstance = new("workflow.instance.read");
    public static readonly PermissionId CancelInstance = new("workflow.instance.cancel");
}

public enum AuthenticationStatus { Succeeded, Rejected, Unavailable, LockedOut }
public sealed record AuthenticationResult(AuthenticationStatus Status, IdentityReference? Identity = null, string? ErrorCode = null);
public sealed record PasswordCredential(string UserName, string Password);

public sealed record DirectoryIdentity(
    IdentityReference Identity,
    string DisplayName,
    string? Email,
    IReadOnlySet<string> GroupIds);

public enum ProviderOutcome { Succeeded, NotFound, Conflict, Forbidden, Unavailable }
public sealed record ProviderResult<T>(ProviderOutcome Outcome, T? Value = default, string? ErrorCode = null);
public sealed record AuthorizationRequest(IdentityReference Identity, PermissionId Permission, string? ResourceType = null, string? ResourceId = null);
public sealed record AuthorizationDecision(bool Allowed, string ReasonCode, bool ProviderUnavailable = false);

public sealed record SecurityProfile(string Id, string DisplayName, IReadOnlySet<PermissionId> Permissions, bool GrantsAdministrativeAccess);
public sealed record GroupReference(string ProviderId, string GroupId);
public sealed record SecurityAuditEntry(DateTimeOffset OccurredAtUtc, IdentityReference Actor, string Action, string Target, string Outcome);
public sealed record SecuritySession(
    string TokenDigest,
    IdentityReference Identity,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset RemoteStatusCheckedAtUtc,
    bool Revoked,
    long Revision);

public enum SessionValidationStatus { Valid, NotFound, Expired, Revoked, RevalidationRequired, Unavailable }
public sealed record SessionValidationResult(SessionValidationStatus Status, IdentityReference? Identity = null, string? ErrorCode = null);
public sealed record SessionIssueResult(string Token, DateTimeOffset ExpiresAtUtc);

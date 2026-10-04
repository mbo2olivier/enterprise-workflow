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

public readonly record struct WorkflowActionId
{
    public WorkflowActionId(string value) => Value = RequiredIdentifier(value, nameof(value), 160);
    public string Value { get; }
    public override string ToString() => Value;

    private static string RequiredIdentifier(string value, string parameter, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || !IsAsciiLetterOrDigit(value[0]) ||
            value.Any(character => !IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException($"A workflow action identifier must contain 1 to {maximumLength} ASCII letters, digits, dots, hyphens, or underscores and start with a letter or digit.", parameter);
        return value;
    }

    private static bool IsAsciiLetterOrDigit(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}

public static class WorkflowActions
{
    public static readonly WorkflowActionId Start = new("workflow.start");
    public static readonly WorkflowActionId ReadInstance = new("instance.read");
    public static readonly WorkflowActionId CancelInstance = new("instance.cancel");
    public static readonly WorkflowActionId ReadTask = new("task.read");
    public static readonly WorkflowActionId AssignTask = new("task.assign");
    public static readonly WorkflowActionId ClaimTask = new("task.claim");
    public static readonly WorkflowActionId SubmitTask = new("task.submit");
    public static readonly WorkflowActionId ApproveTask = new("task.approve");
    public static readonly WorkflowActionId RejectTask = new("task.reject");
}

public sealed record WorkflowAuthorizationScope
{
    public WorkflowAuthorizationScope(string workflowId, int definitionVersion, string? nodeId, WorkflowActionId action)
    {
        WorkflowId = RequiredTechnicalId(workflowId, nameof(workflowId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(definitionVersion);
        DefinitionVersion = definitionVersion;
        NodeId = nodeId is null ? null : RequiredTechnicalId(nodeId, nameof(nodeId));
        Action = action.Value.Length == 0 ? throw new ArgumentException("A workflow action is required.", nameof(action)) : action;
    }

    public string WorkflowId { get; }
    public int DefinitionVersion { get; }
    public string? NodeId { get; }
    public WorkflowActionId Action { get; }

    private static string RequiredTechnicalId(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || !IsAsciiLetterOrDigit(value[0]) ||
            value.Any(character => !IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException("A workflow or node identifier must contain 1 to 128 ASCII letters, digits, dots, hyphens, or underscores and start with a letter or digit.", parameter);
        return value;
    }

    private static bool IsAsciiLetterOrDigit(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}

public enum WorkflowGrantRecipientKind { Identity, Profile }

public sealed record WorkflowGrantRecipient
{
    private WorkflowGrantRecipient(WorkflowGrantRecipientKind kind, IdentityReference? identity, string? profileId)
    {
        Kind = kind;
        Identity = identity;
        ProfileId = profileId;
    }

    public WorkflowGrantRecipientKind Kind { get; }
    public IdentityReference? Identity { get; }
    public string? ProfileId { get; }

    public static WorkflowGrantRecipient ForIdentity(IdentityReference identity) => new(WorkflowGrantRecipientKind.Identity, identity, null);
    public static WorkflowGrantRecipient ForProfile(string profileId) => new(WorkflowGrantRecipientKind.Profile, null,
        !string.IsNullOrWhiteSpace(profileId) && profileId.Length <= 160 ? profileId : throw new ArgumentException("A profile identifier is required and limited to 160 characters.", nameof(profileId)));
}

public sealed record WorkflowAccessGrant(WorkflowAuthorizationScope Scope, WorkflowGrantRecipient Recipient, long PolicyRevision = 0);
public sealed record WorkflowAccessEvaluation(bool Allowed, long PolicyRevision);
public sealed record WorkflowAuthorizationRequest(IdentityReference Identity, WorkflowAuthorizationScope Scope);
public sealed record WorkflowActionDescriptor(WorkflowAuthorizationScope Scope);

public enum AuthenticationStatus { Succeeded, Rejected, Unavailable, LockedOut }
public sealed record AuthenticationResult(AuthenticationStatus Status, IdentityReference? Identity = null, string? ErrorCode = null);
public sealed record PasswordCredential(string UserName, string Password);

[Flags]
public enum DirectoryCapabilities
{
    None = 0,
    Search = 1,
    Groups = 2,
    AccountStatus = 4,
}

public enum DirectoryAccountStatus { Unknown, Enabled, Disabled }

public sealed record DirectoryIdentity(
    IdentityReference Identity,
    string DisplayName,
    string? Email,
    IReadOnlySet<string> GroupIds,
    DirectoryAccountStatus AccountStatus = DirectoryAccountStatus.Unknown);

public enum ProviderOutcome { Succeeded, NotFound, Conflict, Forbidden, Unavailable }
public sealed record ProviderResult<T>(ProviderOutcome Outcome, T? Value = default, string? ErrorCode = null);
public sealed record AuthorizationRequest(IdentityReference Identity, PermissionId Permission, string? ResourceType = null, string? ResourceId = null);
public sealed record AuthorizationDecision(bool Allowed, string ReasonCode, bool ProviderUnavailable = false, long? PolicyRevision = null);

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

namespace EnterpriseWorkflow.Security;

public interface IAuthenticationProvider
{
    string ProviderId { get; }
    ValueTask<AuthenticationResult> AuthenticateAsync(PasswordCredential credential, CancellationToken cancellationToken);
}

public interface IIdentityDirectory
{
    string ProviderId { get; }
    DirectoryCapabilities Capabilities { get; }
    ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(IdentityReference identity, bool includeGroups, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(string query, int maximumResults, CancellationToken cancellationToken);
}

public interface IAuthorizationProvider
{
    ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}

public interface IWorkflowAuthorizationService
{
    ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken);
}

public interface IWorkflowActionCatalog
{
    ValueTask<bool> ContainsAsync(WorkflowAuthorizationScope scope, CancellationToken cancellationToken);
}

public interface IWorkflowAccessStore
{
    ValueTask<ProviderResult<WorkflowAccessGrant>> GrantAsync(WorkflowAccessGrant grant, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> RevokeAsync(WorkflowAuthorizationScope scope, WorkflowGrantRecipient recipient, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IReadOnlyList<WorkflowAccessGrant>>> ListAsync(string workflowId, int definitionVersion, CancellationToken cancellationToken);
    ValueTask<ProviderResult<WorkflowAccessEvaluation>> ResolveAsync(IdentityReference identity, IReadOnlySet<string>? currentGroupIds, WorkflowAuthorizationScope scope, CancellationToken cancellationToken);
}

public interface ISecurityProfileStore
{
    ValueTask<ProviderResult<IReadOnlyList<SecurityProfile>>> ListProfilesAsync(int maximumResults, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IReadOnlyList<SecurityIdentityProfileAssignment>>> ListIdentityAssignmentsAsync(int maximumResults, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IReadOnlyList<SecurityGroupProfileMapping>>> ListGroupMappingsAsync(int maximumResults, CancellationToken cancellationToken);
    ValueTask<ProviderResult<SecurityProfile>> UpsertProfileAsync(SecurityProfile profile, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> DeleteProfileAsync(string profileId, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> AssignIdentityAsync(IdentityReference identity, string profileId, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> UnassignIdentityAsync(IdentityReference identity, string profileId, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> MapGroupAsync(GroupReference group, string profileId, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> UnmapGroupAsync(GroupReference group, string profileId, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IReadOnlySet<PermissionId>>> ResolvePermissionsAsync(IdentityReference identity, IReadOnlySet<string>? currentGroupIds, CancellationToken cancellationToken);
    ValueTask<ProviderResult<int>> CountAdministrativeIdentitiesAsync(CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<SecurityAuditEntry>> ReadAuditAsync(int maximumResults, CancellationToken cancellationToken);
}

public interface ILocalAccountStore
{
    ValueTask<ProviderResult<IReadOnlyList<LocalAccount>>> ListAsync(int maximumResults, CancellationToken cancellationToken);
    ValueTask<ProviderResult<LocalAccount>> FindByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken);
    ValueTask<ProviderResult<LocalAccount>> FindByIdentityAsync(IdentityReference identity, CancellationToken cancellationToken);
    ValueTask<ProviderResult<LocalAccount>> CreateAsync(LocalAccount account, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<LocalAccount>> UpdateAsync(LocalAccount account, CancellationToken cancellationToken);
    ValueTask<ProviderResult<LocalAccount>> RecordAuthenticationFailureAsync(IdentityReference identity, int maximumAttempts, DateTimeOffset lockoutEndUtc, CancellationToken cancellationToken);
    ValueTask<ProviderResult<LocalAccount>> RecordAuthenticationSuccessAsync(IdentityReference identity, string? upgradedPasswordHash, CancellationToken cancellationToken);
}

public interface ISecuritySessionStore
{
    ValueTask<ProviderResult<SecuritySession>> CreateSessionAsync(SecuritySession session, CancellationToken cancellationToken);
    ValueTask<ProviderResult<SecuritySession>> FindSessionAsync(string tokenDigest, CancellationToken cancellationToken);
    ValueTask<ProviderResult<SecuritySession>> UpdateSessionAsync(SecuritySession session, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> RevokeSessionAsync(string tokenDigest, IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<ProviderResult<int>> RevokeIdentitySessionsAsync(IdentityReference identity, IdentityReference actor, CancellationToken cancellationToken);
}

public interface ILocalSecurityProvisioningStore
{
    ValueTask<ProviderResult<bool>> BootstrapAsync(LocalAccount account, SecurityProfile administratorProfile, CancellationToken cancellationToken);
    ValueTask<ProviderResult<bool>> RecoverAdministrativeAccessAsync(IdentityReference identity, string administratorProfileId, CancellationToken cancellationToken);
    ValueTask<ProviderResult<LocalAccount>> ChangeLocalAccountAsync(IdentityReference identity, bool enabled, string? passwordHash, IdentityReference actor, CancellationToken cancellationToken);
}

public interface IPresentationSettingsStore
{
    ValueTask<ProviderResult<PresentationSettings>> ReadAsync(CancellationToken cancellationToken);
    ValueTask<ProviderResult<PresentationSettings>> UpdateAsync(
        PresentationSettings settings, IdentityReference actor, CancellationToken cancellationToken);
}

public sealed record LocalAccount(
    IdentityReference Identity,
    string UserName,
    string NormalizedUserName,
    string PasswordHash,
    bool Enabled,
    int FailedAccessCount,
    DateTimeOffset? LockoutEndUtc,
    long Revision);

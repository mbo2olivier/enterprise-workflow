using System.Net;
using Xunit;

namespace EnterpriseWorkflow.Security.Tests;

public sealed class SecurityProviderTests
{
    [Fact]
    public void SecurityPolicyRejectsIncoherentConfiguration()
    {
        var options = new SecurityPolicyOptions
        {
            IdleSessionTimeout = TimeSpan.FromHours(9),
            AbsoluteSessionLifetime = TimeSpan.FromHours(8),
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void LdapConfigurationIsGenericStrictAndCapabilityDriven()
    {
        var options = new LdapDirectoryOptions
        {
            ProviderId = "corporate-ldap",
            Host = "ldap.example.test",
            BaseDn = "dc=example,dc=test",
            AccountStatusAttribute = "employeeType",
            EnabledAccountStatusValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "active" },
            GroupResolution = LdapGroupResolutionStrategy.GroupSearch,
            GroupBaseDn = "ou=groups,dc=example,dc=test",
            GroupObjectFilter = "(objectClass=groupOfUniqueNames)",
            GroupMemberAttribute = "uniqueMember",
        };

        options.Validate();
        var provider = new LdapDirectoryProvider(options, new StaticLdapCredentialProvider());

        Assert.Equal(LdapTransportMode.Ldaps, options.Transport);
        Assert.Null(options.CertificateDirectory);
        Assert.Equal("entryUUID", options.SubjectAttribute);
        Assert.Equal(DirectoryCapabilities.Search | DirectoryCapabilities.Groups | DirectoryCapabilities.AccountStatus, provider.Capabilities);
    }

    [Fact]
    public void LdapConfigurationRejectsFilterInjectionAndAccidentalPlainTextSetup()
    {
        Assert.Throws<InvalidOperationException>(() => new LdapDirectoryOptions
        {
            ProviderId = "ldap",
            Host = "localhost",
            BaseDn = "dc=example,dc=test",
            UserObjectFilter = "objectClass=*",
        }.Validate());
        Assert.Throws<InvalidOperationException>(() => new LdapDirectoryOptions
        {
            ProviderId = "ldap",
            Host = "localhost",
            BaseDn = "dc=example,dc=test",
            Transport = LdapTransportMode.PlainText,
            PlainTextPort = 0,
        }.Validate());
    }

    [Fact]
    public void LdapIdentifierCodecsRoundTripStableValuesIntoSafeFilters()
    {
        var guid = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
        var guidBytes = guid.ToByteArray();
        var bytes = new byte[] { 0, 42, 92, 255 };

        Assert.Equal(guid.ToString("N"), LdapDirectoryProvider.Decode(guidBytes, LdapAttributeValueCodec.GuidLittleEndian));
        Assert.Equal(string.Concat(guidBytes.Select(value => $"\\{value:x2}")),
            LdapDirectoryProvider.EncodeFilterValue(guid.ToString("N"), LdapAttributeValueCodec.GuidLittleEndian));
        Assert.Equal("002a5cff", LdapDirectoryProvider.Decode(bytes, LdapAttributeValueCodec.Hexadecimal));
        Assert.Equal("\\00\\2a\\5c\\ff", LdapDirectoryProvider.EncodeFilterValue("002a5cff", LdapAttributeValueCodec.Hexadecimal));
        Assert.Equal(Convert.ToBase64String(bytes), LdapDirectoryProvider.Decode(bytes, LdapAttributeValueCodec.Base64));
        Assert.Equal("\\00\\2a\\5c\\ff", LdapDirectoryProvider.EncodeFilterValue(Convert.ToBase64String(bytes), LdapAttributeValueCodec.Base64));
        Assert.Equal("alice\\2a\\28admin\\29", LdapDirectoryProvider.EncodeFilterValue("alice*(admin)", LdapAttributeValueCodec.Utf8String));
    }

    [Fact]
    public async Task LocalAuthenticationHashesPasswordsAndEnforcesConfiguredLockout()
    {
        var identity = new IdentityReference(LocalAuthenticationProvider.LocalProviderId, "account-1");
        var account = new LocalAccount(identity, "Alice", "ALICE", "pending", true, 0, null, 0);
        var store = new AccountStore(account);
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        var policy = new SecurityPolicyOptions { MaximumFailedAccessAttempts = 2, LockoutDuration = TimeSpan.FromMinutes(10) };
        var provider = new LocalAuthenticationProvider(store, policy, time);
        store.Account = account with { PasswordHash = provider.HashPassword(account, "correct horse") };

        var rejected = await provider.AuthenticateAsync(new PasswordCredential("alice", "wrong"), TestContext.Current.CancellationToken);
        var locked = await provider.AuthenticateAsync(new PasswordCredential("alice", "wrong again"), TestContext.Current.CancellationToken);
        var stillLocked = await provider.AuthenticateAsync(new PasswordCredential("alice", "correct horse"), TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticationStatus.Rejected, rejected.Status);
        Assert.Equal(AuthenticationStatus.LockedOut, locked.Status);
        Assert.Equal(AuthenticationStatus.LockedOut, stillLocked.Status);
        Assert.Equal(2, store.Account.FailedAccessCount);
    }

    [Fact]
    public async Task DirectProfileRemainsUsableWhenOptionalLdapGroupsAreUnavailable()
    {
        var identity = new IdentityReference("ad", "subject-1");
        var store = new ProfileStore(identity, WorkflowPermissions.Approve);
        var provider = new InternalProfileAuthorizationProvider(store,
            new Dictionary<string, IIdentityDirectory> { ["ad"] = new UnavailableDirectory() });

        var decision = await provider.AuthorizeAsync(
            new AuthorizationRequest(identity, WorkflowPermissions.Approve), TestContext.Current.CancellationToken);

        Assert.True(decision.Allowed);
        Assert.Equal("security.direct-profile", decision.ReasonCode);
        Assert.Null(store.LastGroups);
    }

    [Fact]
    public async Task ContextualAuthorizationUsesExactScopeAndDirectGrantDuringDirectoryOutage()
    {
        var identity = new IdentityReference("ad", "subject-1");
        var scope = new WorkflowAuthorizationScope("leave-request", 4, "manager", WorkflowActions.ApproveTask);
        var store = new WorkflowAccessStore(identity, scope);
        var service = new InternalWorkflowAuthorizationService(store,
            new Dictionary<string, IIdentityDirectory> { ["ad"] = new UnavailableDirectory() });

        var allowed = await service.AuthorizeAsync(new(identity, scope), TestContext.Current.CancellationToken);
        var wrongAction = await service.AuthorizeAsync(new(identity,
            new("leave-request", 4, "manager", WorkflowActions.RejectTask)), TestContext.Current.CancellationToken);

        Assert.True(allowed.Allowed);
        Assert.Equal("security.workflow-direct-grant", allowed.ReasonCode);
        Assert.Equal(7, allowed.PolicyRevision);
        Assert.False(wrongAction.Allowed);
        Assert.True(wrongAction.ProviderUnavailable);
    }

    [Fact]
    public async Task WorkflowAccessAdministrationRejectsUndeclaredActions()
    {
        var actor = new IdentityReference(SecurityProviderIds.Local, "administrator");
        var declared = new WorkflowAuthorizationScope("leave-request", 1, "approval", WorkflowActions.ApproveTask);
        var store = new WorkflowAccessStore(actor, declared);
        var administration = new WorkflowAccessAdministration(new AllowAuthorizationProvider(), store,
            new WorkflowActionCatalog([new(declared)]));

        var accepted = await administration.GrantAsync(actor, declared, WorkflowGrantRecipient.ForIdentity(actor), TestContext.Current.CancellationToken);
        var refused = await administration.GrantAsync(actor,
            new("leave-request", 1, "approval", WorkflowActions.RejectTask), WorkflowGrantRecipient.ForIdentity(actor), TestContext.Current.CancellationToken);

        Assert.Equal(ProviderOutcome.Succeeded, accepted.Outcome);
        Assert.Equal(ProviderOutcome.NotFound, refused.Outcome);
        Assert.Equal("security.workflow-action-not-declared", refused.ErrorCode);
        Assert.Equal(1, store.GrantCalls);
    }

    [Fact]
    public async Task CandidateSearchFiltersWithTheExactWorkflowScope()
    {
        var requester = new IdentityReference(SecurityProviderIds.Local, "administrator");
        var eligible = new IdentityReference("ldap", "eligible");
        var scope = new WorkflowAuthorizationScope("leave-request", 1, "approval", WorkflowActions.ApproveTask);
        var directory = new SearchDirectory([
            new(eligible, "Eligible", null, new HashSet<string>()),
            new(new("ldap", "not-eligible"), "Not eligible", null, new HashSet<string>()),
        ]);
        var workflowAuthorization = new InternalWorkflowAuthorizationService(new WorkflowAccessStore(eligible, scope),
            new Dictionary<string, IIdentityDirectory>());
        var search = new AuthorizedIdentitySearch(directory, new AllowAuthorizationProvider(), workflowAuthorization);

        var result = await search.SearchAsync(requester, "elig", scope, 10, TestContext.Current.CancellationToken);

        Assert.Equal(ProviderOutcome.Succeeded, result.Outcome);
        Assert.Collection(result.Value!, candidate => Assert.Equal(eligible, candidate.Identity));
    }

    [Fact]
    public async Task ApiAuthenticationRequiresExplicitHttpsPasswordForwardingAndRefusesRedirects()
    {
        Assert.Throws<InvalidOperationException>(() => new ExampleApiAuthenticationProvider(new()
        {
            ProviderId = "api",
            BaseAddress = new Uri("https://identity.example/"),
            EnablePasswordCredentialForwarding = false,
        }));
        using var provider = new ExampleApiAuthenticationProvider(new()
        {
            ProviderId = "api",
            BaseAddress = new Uri("https://identity.example/"),
            EnablePasswordCredentialForwarding = true,
        }, new ResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"authenticated\":true,\"subjectId\":\"api-user-1\"}", System.Text.Encoding.UTF8, "application/json"),
        }));
        var result = await provider.AuthenticateAsync(new("alice", "secret-value"), TestContext.Current.CancellationToken);
        Assert.Equal(AuthenticationStatus.Succeeded, result.Status);
        Assert.Equal(new IdentityReference("api", "api-user-1"), result.Identity);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StaticLdapCredentialProvider : ILdapServiceCredentialProvider
    {
        public ValueTask<NetworkCredential> GetCredentialAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new NetworkCredential("uid=service,dc=example,dc=test", "unused"));
    }

    private sealed class ResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed class AccountStore(LocalAccount account) : ILocalAccountStore
    {
        public LocalAccount Account { get; set; } = account;
        public ValueTask<ProviderResult<LocalAccount>> FindByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            ValueTask.FromResult(string.Equals(Account.NormalizedUserName, normalizedUserName, StringComparison.Ordinal)
                ? new ProviderResult<LocalAccount>(ProviderOutcome.Succeeded, Account)
                : new(ProviderOutcome.NotFound));
        public ValueTask<ProviderResult<LocalAccount>> FindByIdentityAsync(IdentityReference identity, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<LocalAccount>(ProviderOutcome.Succeeded, Account));
        public ValueTask<ProviderResult<LocalAccount>> CreateAsync(LocalAccount value, IdentityReference actor, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<LocalAccount>(ProviderOutcome.Conflict));
        public ValueTask<ProviderResult<LocalAccount>> UpdateAsync(LocalAccount value, CancellationToken cancellationToken)
        {
            Account = value with { Revision = value.Revision + 1 };
            return ValueTask.FromResult(new ProviderResult<LocalAccount>(ProviderOutcome.Succeeded, Account));
        }
        public ValueTask<ProviderResult<LocalAccount>> RecordAuthenticationFailureAsync(IdentityReference identity, int maximumAttempts, DateTimeOffset lockoutEndUtc, CancellationToken cancellationToken) { var failures = Account.FailedAccessCount + 1; Account = Account with { FailedAccessCount = failures, LockoutEndUtc = failures >= maximumAttempts ? lockoutEndUtc : null, Revision = Account.Revision + 1 }; return ValueTask.FromResult(new ProviderResult<LocalAccount>(ProviderOutcome.Succeeded, Account)); }
        public ValueTask<ProviderResult<LocalAccount>> RecordAuthenticationSuccessAsync(IdentityReference identity, string? upgradedPasswordHash, CancellationToken cancellationToken) { Account = Account with { PasswordHash = upgradedPasswordHash ?? Account.PasswordHash, FailedAccessCount = 0, LockoutEndUtc = null, Revision = Account.Revision + 1 }; return ValueTask.FromResult(new ProviderResult<LocalAccount>(ProviderOutcome.Succeeded, Account)); }
    }

    private sealed class UnavailableDirectory : IIdentityDirectory
    {
        public string ProviderId => "ad";
        public DirectoryCapabilities Capabilities => DirectoryCapabilities.Groups | DirectoryCapabilities.Search;
        public ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(IdentityReference identity, bool includeGroups, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<DirectoryIdentity>(ProviderOutcome.Unavailable));
        public ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(string query, int maximumResults, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<IReadOnlyList<DirectoryIdentity>>(ProviderOutcome.Unavailable));
    }

    private sealed class AllowAuthorizationProvider : IAuthorizationProvider
    {
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuthorizationDecision(true, "test.allowed"));
    }

    private sealed class SearchDirectory(IReadOnlyList<DirectoryIdentity> identities) : IIdentityDirectory
    {
        public string ProviderId => "ldap";
        public DirectoryCapabilities Capabilities => DirectoryCapabilities.Search;
        public ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(IdentityReference identity, bool includeGroups, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<DirectoryIdentity>(ProviderOutcome.NotFound));
        public ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(string query, int maximumResults, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<IReadOnlyList<DirectoryIdentity>>(ProviderOutcome.Succeeded, identities.Take(maximumResults).ToList()));
    }

    private sealed class WorkflowAccessStore(IdentityReference allowedIdentity, WorkflowAuthorizationScope allowedScope) : IWorkflowAccessStore
    {
        public int GrantCalls { get; private set; }
        public ValueTask<ProviderResult<WorkflowAccessGrant>> GrantAsync(WorkflowAccessGrant grant, IdentityReference actor, CancellationToken cancellationToken)
        {
            GrantCalls++;
            return ValueTask.FromResult(new ProviderResult<WorkflowAccessGrant>(ProviderOutcome.Succeeded, grant with { PolicyRevision = 8 }));
        }
        public ValueTask<ProviderResult<bool>> RevokeAsync(WorkflowAuthorizationScope scope, WorkflowGrantRecipient recipient, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<IReadOnlyList<WorkflowAccessGrant>>> ListAsync(string workflowId, int definitionVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<WorkflowAccessEvaluation>> ResolveAsync(IdentityReference identity, IReadOnlySet<string>? currentGroupIds, WorkflowAuthorizationScope scope, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<WorkflowAccessEvaluation>(ProviderOutcome.Succeeded, new(identity == allowedIdentity && scope == allowedScope, 7)));
    }

    private sealed class ProfileStore(IdentityReference identity, PermissionId permission) : ISecurityProfileStore
    {
        public IReadOnlySet<string>? LastGroups { get; private set; }
        public ValueTask<ProviderResult<IReadOnlySet<PermissionId>>> ResolvePermissionsAsync(IdentityReference requested, IReadOnlySet<string>? currentGroupIds, CancellationToken cancellationToken)
        {
            LastGroups = currentGroupIds;
            IReadOnlySet<PermissionId> permissions = requested == identity ? new HashSet<PermissionId> { permission } : new HashSet<PermissionId>();
            return ValueTask.FromResult(new ProviderResult<IReadOnlySet<PermissionId>>(ProviderOutcome.Succeeded, permissions));
        }
        public ValueTask<ProviderResult<SecurityProfile>> UpsertProfileAsync(SecurityProfile profile, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<bool>> DeleteProfileAsync(string profileId, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<bool>> AssignIdentityAsync(IdentityReference value, string profileId, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<bool>> UnassignIdentityAsync(IdentityReference value, string profileId, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<bool>> MapGroupAsync(GroupReference group, string profileId, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<bool>> UnmapGroupAsync(GroupReference group, string profileId, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<int>> CountAdministrativeIdentitiesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<SecurityAuditEntry>> ReadAuditAsync(int maximumResults, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

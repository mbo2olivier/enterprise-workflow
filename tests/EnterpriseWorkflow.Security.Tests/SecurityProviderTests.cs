using Xunit;
using System.Net;

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
    public async Task DirectProfileRemainsUsableWhenOptionalAdGroupsAreUnavailable()
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
    public async Task ApiAuthenticationRequiresExplicitHttpsPasswordForwardingAndRefusesRedirects()
    {
        Assert.Throws<InvalidOperationException>(() => new ExampleApiAuthenticationProvider(new()
        {
            ProviderId = "api", BaseAddress = new Uri("https://identity.example/"), EnablePasswordCredentialForwarding = false,
        }));
        using var provider = new ExampleApiAuthenticationProvider(new()
        {
            ProviderId = "api", BaseAddress = new Uri("https://identity.example/"), EnablePasswordCredentialForwarding = true,
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
        public ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(IdentityReference identity, bool includeGroups, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<DirectoryIdentity>(ProviderOutcome.Unavailable));
        public ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(string query, int maximumResults, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<IReadOnlyList<DirectoryIdentity>>(ProviderOutcome.Unavailable));
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

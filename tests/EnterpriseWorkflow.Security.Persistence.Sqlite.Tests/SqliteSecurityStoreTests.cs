using Microsoft.Data.Sqlite;
using Xunit;

namespace EnterpriseWorkflow.Security.Persistence.Sqlite.Tests;

public sealed class SqliteSecurityStoreTests
{
    [Fact]
    public async Task ProfilesAssignmentsPermissionsAuditAndLastAdministratorAreDurable()
    {
        await using var fixture = new Fixture(); await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = fixture.Database.CreateStore();
        var actor = new IdentityReference(SecurityProviderIds.Local, "bootstrap");
        var profile = new SecurityProfile("administrators", "Administrateurs", new HashSet<PermissionId> { WorkflowPermissions.ManageAccess }, true);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.UpsertProfileAsync(profile, actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.AssignIdentityAsync(actor, profile.Id, actor, TestContext.Current.CancellationToken)).Outcome);
        var permissions = await store.ResolvePermissionsAsync(actor, null, TestContext.Current.CancellationToken);
        Assert.Contains(WorkflowPermissions.ManageAccess, permissions.Value!);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.MapGroupAsync(new("ad", "group-1"), profile.Id, actor, TestContext.Current.CancellationToken)).Outcome);
        var groupPermissions = await store.ResolvePermissionsAsync(new("ad", "member-1"), new HashSet<string> { "group-1" }, TestContext.Current.CancellationToken);
        Assert.Contains(WorkflowPermissions.ManageAccess, groupPermissions.Value!);
        var listedProfile = Assert.Single((await store.ListProfilesAsync(10, TestContext.Current.CancellationToken)).Value!);
        Assert.Equal(profile.Id, listedProfile.Id);
        Assert.Equal(profile.DisplayName, listedProfile.DisplayName);
        Assert.Equal(profile.GrantsAdministrativeAccess, listedProfile.GrantsAdministrativeAccess);
        Assert.True(profile.Permissions.SetEquals(listedProfile.Permissions));
        Assert.Equal(actor, Assert.Single((await store.ListIdentityAssignmentsAsync(10, TestContext.Current.CancellationToken)).Value!).Identity);
        Assert.Equal("group-1", Assert.Single((await store.ListGroupMappingsAsync(10, TestContext.Current.CancellationToken)).Value!).Group.GroupId);
        var demoted = await store.UpsertProfileAsync(profile with { GrantsAdministrativeAccess = false }, actor,
            TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Conflict, demoted.Outcome); Assert.Equal("security.last-administrator", demoted.ErrorCode);
        var refused = await store.UnassignIdentityAsync(actor, profile.Id, actor, TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Conflict, refused.Outcome); Assert.Equal("security.last-administrator", refused.ErrorCode);
        Assert.Equal(3, (await store.ReadAuditAsync(10, TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task LocalAccountUsesOptimisticRevision()
    {
        await using var fixture = new Fixture(); await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = fixture.Database.CreateStore(); var actor = new IdentityReference(SecurityProviderIds.Local, "bootstrap");
        var account = new LocalAccount(new(SecurityProviderIds.Local, "one"), "Alice", "ALICE", "hash", true, 0, null, 0);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.CreateAsync(account, actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(account.Identity, Assert.Single((await store.ListAsync(10, TestContext.Current.CancellationToken)).Value!).Identity);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.UpdateAsync(account with { FailedAccessCount = 1 }, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(ProviderOutcome.Conflict, (await store.UpdateAsync(account with { FailedAccessCount = 2 }, TestContext.Current.CancellationToken)).Outcome);
    }

    [Fact]
    public async Task PresentationSettingsAreBoundedAuditedAndUseOptimisticRevision()
    {
        await using var fixture = new Fixture(); await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = fixture.Database.CreateStore(); var actor = new IdentityReference(SecurityProviderIds.Local, "administrator");
        var defaults = (await store.ReadAsync(TestContext.Current.CancellationToken)).Value!;
        var updated = await store.UpdateAsync(defaults with
        {
            DisplayName = "Portail RH", AccentColor = "#2457A7", TimeZoneId = "UTC", Culture = "fr-FR",
        }, actor, TestContext.Current.CancellationToken);

        Assert.Equal(ProviderOutcome.Succeeded, updated.Outcome);
        Assert.Equal(1, updated.Value!.Revision);
        Assert.Equal("Portail RH", (await fixture.Database.CreateStore().ReadAsync(TestContext.Current.CancellationToken)).Value!.DisplayName);
        Assert.Equal("presentation.settings.update", Assert.Single(await store.ReadAuditAsync(10, TestContext.Current.CancellationToken)).Action);
        Assert.Equal(ProviderOutcome.Conflict, (await store.UpdateAsync(defaults, actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal("presentation.settings-invalid", (await store.UpdateAsync(updated.Value with { AccentColor = "red" }, actor,
            TestContext.Current.CancellationToken)).ErrorCode);
    }

    [Fact]
    public async Task BootstrapCannotBeRepeatedAndSessionsCanBeRevoked()
    {
        await using var fixture = new Fixture(); await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = fixture.Database.CreateStore(); var identity = new IdentityReference(SecurityProviderIds.Local, "first-admin");
        var account = new LocalAccount(identity, "Admin", "ADMIN", "hash", true, 0, null, 0);
        var profile = new SecurityProfile("administrators", "Administrateurs", new HashSet<PermissionId> { WorkflowPermissions.ManageAccess }, true);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.BootstrapAsync(account, profile, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal("security.already-bootstrapped", (await store.BootstrapAsync(account, profile, TestContext.Current.CancellationToken)).ErrorCode);
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var session = new SecuritySession(new string('b', 64), identity, now, now, now.AddHours(8), now, false, 0);
        await store.CreateSessionAsync(session, TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.RevokeSessionAsync(session.TokenDigest, identity, TestContext.Current.CancellationToken)).Outcome);
        Assert.True((await store.FindSessionAsync(session.TokenDigest, TestContext.Current.CancellationToken)).Value!.Revoked);
    }

    [Fact]
    public async Task SessionTokenIsOpaqueAndIdleExpirationIsEnforced()
    {
        await using var fixture = new Fixture(); await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var time = new MutableTimeProvider(new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        var manager = new SecuritySessionManager(fixture.Database.CreateStore(time), new SecurityPolicyOptions
        {
            IdleSessionTimeout = TimeSpan.FromMinutes(2), AbsoluteSessionLifetime = TimeSpan.FromHours(1), RemoteStatusRevalidationInterval = TimeSpan.FromMinutes(5),
        }, new Dictionary<string, IIdentityDirectory>(), time);
        var identity = new IdentityReference(SecurityProviderIds.Local, "session-user");
        var issued = await manager.IssueAsync(identity, TestContext.Current.CancellationToken);
        Assert.Equal(64, issued.Value!.Token.Length);
        Assert.Equal(SessionValidationStatus.Valid, (await manager.ValidateAsync(issued.Value.Token, TestContext.Current.CancellationToken)).Status);
        time.UtcNow = time.UtcNow.AddMinutes(3);
        Assert.Equal(SessionValidationStatus.Expired, (await manager.ValidateAsync(issued.Value.Token, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task WorkflowGrantsAreExactVersionedRevocableAndDurable()
    {
        await using var fixture = new Fixture(); await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = fixture.Database.CreateStore();
        var actor = new IdentityReference(SecurityProviderIds.Local, "administrator");
        var candidate = new IdentityReference("ldap", "stable-user-1");
        var approve = new WorkflowAuthorizationScope("leave-request", 2, "manager-approval", WorkflowActions.ApproveTask);
        var recipient = WorkflowGrantRecipient.ForIdentity(candidate);

        var granted = await store.GrantAsync(new(approve, recipient), actor, TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Succeeded, granted.Outcome);
        Assert.Equal(1, granted.Value!.PolicyRevision);
        Assert.True((await store.ResolveAsync(candidate, null, approve, TestContext.Current.CancellationToken)).Value!.Allowed);
        Assert.False((await store.ResolveAsync(candidate, null, new("leave-request", 3, "manager-approval", WorkflowActions.ApproveTask), TestContext.Current.CancellationToken)).Value!.Allowed);
        Assert.False((await store.ResolveAsync(candidate, null, new("leave-request", 2, "manager-approval", WorkflowActions.RejectTask), TestContext.Current.CancellationToken)).Value!.Allowed);
        Assert.False((await store.ResolveAsync(candidate, null, new("leave-request", 2, "finance-review", WorkflowActions.ApproveTask), TestContext.Current.CancellationToken)).Value!.Allowed);

        Assert.Equal(ProviderOutcome.Succeeded, (await store.RevokeAsync(approve, recipient, actor, TestContext.Current.CancellationToken)).Outcome);
        var revoked = await store.ResolveAsync(candidate, null, approve, TestContext.Current.CancellationToken);
        Assert.False(revoked.Value!.Allowed);
        Assert.Equal(2, revoked.Value.PolicyRevision);
        Assert.Empty((await store.ListAsync("leave-request", 2, TestContext.Current.CancellationToken)).Value!);
    }

    [Fact]
    public async Task WorkflowProfileGrantUsesDirectAndCurrentGroupMembership()
    {
        await using var fixture = new Fixture(); await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = fixture.Database.CreateStore(); var actor = new IdentityReference(SecurityProviderIds.Local, "administrator");
        var profile = new SecurityProfile("leave-managers", "Responsables congés", new HashSet<PermissionId>(), false);
        await store.UpsertProfileAsync(profile, actor, TestContext.Current.CancellationToken);
        var direct = new IdentityReference("ldap", "direct-manager");
        await store.AssignIdentityAsync(direct, profile.Id, actor, TestContext.Current.CancellationToken);
        await store.MapGroupAsync(new("ldap", "manager-group"), profile.Id, actor, TestContext.Current.CancellationToken);
        var scope = new WorkflowAuthorizationScope("leave-request", 1, "approval", WorkflowActions.ApproveTask);
        await store.GrantAsync(new(scope, WorkflowGrantRecipient.ForProfile(profile.Id)), actor, TestContext.Current.CancellationToken);

        Assert.True((await store.ResolveAsync(direct, null, scope, TestContext.Current.CancellationToken)).Value!.Allowed);
        Assert.True((await store.ResolveAsync(new("ldap", "group-manager"), new HashSet<string> { "manager-group" }, scope, TestContext.Current.CancellationToken)).Value!.Allowed);
        Assert.False((await store.ResolveAsync(new("ldap", "former-manager"), new HashSet<string>(), scope, TestContext.Current.CancellationToken)).Value!.Allowed);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ew-security-{Guid.NewGuid():N}.db");
        public SqliteSecurityDatabase Database { get; }
        public Fixture() { var cs = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString(); Database = new(cs); }
        public ValueTask DisposeAsync() { SqliteConnection.ClearAllPools(); foreach (var path in new[] { _path, _path + "-wal", _path + "-shm" }) if (File.Exists(path)) File.Delete(path); return ValueTask.CompletedTask; }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}

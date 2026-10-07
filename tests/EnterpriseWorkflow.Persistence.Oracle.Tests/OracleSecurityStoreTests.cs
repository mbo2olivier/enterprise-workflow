using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence.Oracle;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnterpriseWorkflow.Persistence.Oracle.Tests;

[Collection("Oracle integration")]
public sealed class OracleSecurityStoreTests
{
    [Fact]
    public async Task SecurityMigrationProfilesAccountsSessionsAndAuditAreDurable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING must target a dedicated disposable test schema.");
        var database = new OracleSecurityDatabase(connectionString);
        await using (var context = database.CreateDbContext()) await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = database.CreateStore(); var actor = new IdentityReference(SecurityProviderIds.Local, "oracle-admin");
        var profile = new SecurityProfile("administrators", "Administrateurs", new HashSet<PermissionId> { WorkflowPermissions.ManageAccess }, true);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.UpsertProfileAsync(profile, actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.AssignIdentityAsync(actor, profile.Id, actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.Contains(WorkflowPermissions.ManageAccess, (await store.ResolvePermissionsAsync(actor, null, TestContext.Current.CancellationToken)).Value!);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.MapGroupAsync(new("ad", "oracle-group"), profile.Id, actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.Contains(WorkflowPermissions.ManageAccess, (await store.ResolvePermissionsAsync(new("ad", "member"), new HashSet<string> { "oracle-group" }, TestContext.Current.CancellationToken)).Value!);
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var session = new SecuritySession(new string('a', 64), actor, now, now, now.AddHours(8), now, false, 0);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.CreateSessionAsync(session, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(actor, (await store.FindSessionAsync(session.TokenDigest, TestContext.Current.CancellationToken)).Value!.Identity);
        var scope = new WorkflowAuthorizationScope("oracle-approval", 1, "manager", WorkflowActions.ApproveTask);
        var recipient = WorkflowGrantRecipient.ForIdentity(actor);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.GrantAsync(new(scope, recipient), actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.True((await store.ResolveAsync(actor, null, scope, TestContext.Current.CancellationToken)).Value!.Allowed);
        Assert.False((await store.ResolveAsync(actor, null, new("oracle-approval", 1, "manager", WorkflowActions.RejectTask), TestContext.Current.CancellationToken)).Value!.Allowed);
        Assert.Equal(ProviderOutcome.Succeeded, (await store.RevokeAsync(scope, recipient, actor, TestContext.Current.CancellationToken)).Outcome);
        Assert.False((await store.ResolveAsync(actor, null, scope, TestContext.Current.CancellationToken)).Value!.Allowed);
        var settings = (await store.ReadAsync(TestContext.Current.CancellationToken)).Value!;
        var updatedSettings = await store.UpdateAsync(settings with
        {
            DisplayName = "Oracle Workflow", AccentColor = "#2457A7", TimeZoneId = "UTC", Culture = "fr-FR",
        }, actor, TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Succeeded, updatedSettings.Outcome);
        Assert.Equal("Oracle Workflow", (await database.CreateStore().ReadAsync(TestContext.Current.CancellationToken)).Value!.DisplayName);
        Assert.Equal(6, (await store.ReadAuditAsync(10, TestContext.Current.CancellationToken)).Count);
    }
}

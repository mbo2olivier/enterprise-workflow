using EnterpriseWorkflow.Hosting;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class HostingTests
{
    [Fact]
    public async Task EnabledSecondFactorWithoutHandlerFailsBeforeDatabaseAccess()
    {
        var builder = EnterpriseWorkflowApplication.CreateBuilder([]);
        builder.Configuration["Security:SecondFactor:Enabled"] = "true";
        builder.Configuration["Database:Provider"] = "must-not-be-used";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync());
        Assert.Contains("handler", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HeadlessHostNeedsNoSecondFactorAndPersistsPreferencesForExternalIdentities()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var builder = EnterpriseWorkflowApplication.CreateBuilder([]);
            builder.Configuration["ConnectionStrings:EnterpriseWorkflow"] = $"Data Source={Path.Combine(root, "test.db")};Pooling=False";
            await builder.MigrateAsync();
            await using var host = await builder.BuildAsync();
            Assert.Null(host.Services.GetService<ISecondFactorHandler>());
            var store = host.Services.GetRequiredService<IUserCulturePreferenceStore>();
            var identity = new IdentityReference("external", "alice");
            await store.SetCultureAsync(identity, "en", TestContext.Current.CancellationToken);
            Assert.Equal("en", await store.ReadCultureAsync(identity, TestContext.Current.CancellationToken));
            await store.SetCultureAsync(identity, null, TestContext.Current.CancellationToken);
            Assert.Null(await store.ReadCultureAsync(identity, TestContext.Current.CancellationToken));
            Assert.Throws<InvalidOperationException>(() => host.MapEnterpriseWorkflow());
            await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExternalSessionsWithoutRemoteStatusRemainLocalButPendingAdmissionIsDenied()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var db = new SqliteSecurityDatabase($"Data Source={Path.Combine(root, "security.db")};Pooling=False");
            await db.MigrateAsync(TestContext.Current.CancellationToken);
            var store = db.CreateStore(TimeProvider.System);
            var identity = new IdentityReference("external", "alice");
            var clock = new FutureClock();
            var admission = new Admission();
            var manager = new SecuritySessionManager(store, new(), new Dictionary<string, IIdentityDirectory>(), clock, [], [admission]);
            Assert.False(await manager.CanIssueAsync(identity, TestContext.Current.CancellationToken));
            Assert.Equal(ProviderOutcome.Forbidden, (await manager.IssueAsync(identity, TestContext.Current.CancellationToken)).Outcome);
            admission.Active = true;
            var issued = await manager.IssueAsync(identity, TestContext.Current.CancellationToken);
            clock.Now = clock.Now.AddMinutes(6);
            Assert.Equal(SessionValidationStatus.Valid, (await manager.ValidateAsync(issued.Value!.Token, TestContext.Current.CancellationToken)).Status);
            admission.Active = false;
            Assert.Equal(SessionValidationStatus.Revoked, (await manager.ValidateAsync(issued.Value.Token, TestContext.Current.CancellationToken)).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task EnablingSecondFactorRequiresReconnectionForOldSimpleSessions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-assurance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var db = new SqliteSecurityDatabase($"Data Source={Path.Combine(root, "security.db")};Pooling=False");
            await db.MigrateAsync(TestContext.Current.CancellationToken);
            var store = db.CreateStore(TimeProvider.System);
            var policy = new SecondFactorOptions();
            var sessions = new SecuritySessionManager(store, new(), new Dictionary<string, IIdentityDirectory>(), TimeProvider.System, [], [], policy);
            var identity = new IdentityReference("local", "alice");
            var simple = await sessions.IssueAsync(identity, TestContext.Current.CancellationToken);
            policy.Enabled = true;
            Assert.Equal(SessionValidationStatus.RevalidationRequired, (await sessions.ValidateAsync(simple.Value!.Token, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(ProviderOutcome.Forbidden, (await sessions.IssueAsync(identity, TestContext.Current.CancellationToken)).Outcome);
            var verified = await sessions.IssueAsync(identity, true, TestContext.Current.CancellationToken);
            Assert.Equal(SessionValidationStatus.Valid, (await sessions.ValidateAsync(verified.Value!.Token, TestContext.Current.CancellationToken)).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FutureClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Admission : IIdentityAdmissionProvider
    {
        public string ProviderId => "external";
        public bool Active { get; set; }
        public ValueTask<ProviderResult<IdentityAdmissionStatus>> GetAdmissionAsync(IdentityReference identity, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<IdentityAdmissionStatus>(ProviderOutcome.Succeeded, Active ? IdentityAdmissionStatus.Active : IdentityAdmissionStatus.PendingApproval));
    }
}

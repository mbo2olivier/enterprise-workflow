using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Sdk;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Cli.Tests;

public sealed class DevelopmentSeedTests
{
    [Fact]
    public async Task RestartPreservesRevocationAndChangedSeedDoesNotOverwritePermissions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-seed-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = new SqliteSecurityDatabase(new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "security.db"), Pooling = false }.ToString());
            await database.MigrateAsync(TestContext.Current.CancellationToken);
            var store = database.CreateStore(TimeProvider.System);
            var services = new ServiceCollection();
            services.AddSingleton<ISecurityProfileStore>(store);
            services.AddSingleton<ILocalAccountStore>(store);
            services.AddSingleton<ILocalSecurityProvisioningStore>(store);
            services.AddSingleton(new LocalAuthenticationProvider(store, new SecurityPolicyOptions(), TimeProvider.System));
            services.AddSingleton<IWorkflowPresentationCatalog>(new Catalog());
            await using var provider = services.BuildServiceProvider();
            var config = new DevelopmentConfiguration { Seed = new() { Users = [new() { Id = "alice", DisplayName = "Alice", Permissions = ["test.request"] }] } };
            await DevelopmentSeed.ApplyAsync(provider, config, root);
            var identity = new IdentityReference(SecurityProviderIds.Local, "ew-dev-alice");
            Assert.Contains(new PermissionId("test.request"), (await store.ResolvePermissionsAsync(identity, null, TestContext.Current.CancellationToken)).Value!);
            await store.UnassignIdentityAsync(identity, "ew-dev-alice", identity, TestContext.Current.CancellationToken);
            await DevelopmentSeed.ApplyAsync(provider, config, root);
            Assert.Empty((await store.ResolvePermissionsAsync(identity, null, TestContext.Current.CancellationToken)).Value!);
            config.Seed.Users[0].DisplayName = "Changed";
            await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeed.ApplyAsync(provider, config, root));
            Assert.Empty((await store.ResolvePermissionsAsync(identity, null, TestContext.Current.CancellationToken)).Value!);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class Catalog : IWorkflowPresentationCatalog
    {
        public ImmutableArray<WorkflowProcessDescriptor> Processes => [];
        public ImmutableArray<WorkflowPermissionDefinition> Permissions => [new(new("test.request"), "Request")];
        public bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process) { process = null; return false; }
        public bool TryResolveForm(WorkflowDefinition definition, WorkflowFormReference reference, out WorkflowFormRegistration? registration) { registration = null; return false; }
    }
}

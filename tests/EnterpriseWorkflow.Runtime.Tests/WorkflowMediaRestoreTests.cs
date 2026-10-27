using System.Text;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class WorkflowMediaRestoreTests
{
    [Fact]
    public async Task RestoringDatabaseAndPrivateVolumePreservesAttachedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-media-restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var token = TestContext.Current.CancellationToken;
        try
        {
            var database = new SqliteWorkflowDatabase("Data Source=" + Path.Combine(root, "source.db"));
            await database.MigrateAsync(token);
            var store = new SqliteWorkflowStore(database);
            var files = new FileWorkflowMediaStorageProvider(Path.Combine(root, "objects"));
            var inspector = new PendingInspector();
            var service = new WorkflowMediaService(store, files, [], inspectors: [inspector]);
            var actor = new ActorIdentity(new("local"), "alice");
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("Restauration L9b"));
            var staged = await service.StageAsync(new(actor, null, null, "submission"), "file", "preuve.txt", input,
                new(1024, ["text/plain"], [".txt"]), new(false, []), token);
            Assert.Equal(WorkflowMediaStatus.Quarantined, staged.Status);
            Assert.Equal(staged.Reference, inspector.Reference);
            Assert.Equal(WorkflowMediaStatus.Quarantined, (await store.ReadMediaAsync(staged.Reference, token)).Value!.Status);
            Assert.True(await service.CompleteInspectionAsync(staged.Reference, staged.Sha256, true, token));
            var definition = WorkflowBuilder.Create("restore", 1).Start("start").End("end").Then("start", "end").Validate().Definition!;
            var published = await store.PublishDefinitionAsync(new(definition), token);
            var start = await store.StartInstanceAsync(new(new(new("installation"), new("workflow.start"), actor), new("submission"), "content",
                published.Value!, WorkflowState.Create("{}", 1), null, null, DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), Media: [staged.Reference]), token);
            Assert.Equal(StoreOutcome.Succeeded, start.Outcome);
            await using (var source = new SqliteConnection(database.ConnectionString))
            await using (var target = new SqliteConnection("Data Source=" + Path.Combine(root, "restored.db")))
            {
                await source.OpenAsync(token);
                await target.OpenAsync(token);
                source.BackupDatabase(target);
            }
            var restoredRoot = Path.Combine(root, "restored-objects");
            Directory.CreateDirectory(restoredRoot);
            foreach (var file in Directory.GetFiles(Path.Combine(root, "objects"))) File.Copy(file, Path.Combine(restoredRoot, Path.GetFileName(file)));
            var restored = new WorkflowMediaService(new SqliteWorkflowStore(new SqliteWorkflowDatabase("Data Source=" + Path.Combine(root, "restored.db"))),
                new FileWorkflowMediaStorageProvider(restoredRoot), []);
            await using var opened = await restored.OpenForInstanceAsync(start.Value!.InstanceId, staged.Reference, token);
            using var reader = new StreamReader(opened);
            Assert.Equal("Restauration L9b", await reader.ReadToEndAsync(token));
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
    private sealed class PendingInspector : IWorkflowMediaInspector
    {
        public WorkflowMediaReference? Reference { get; private set; }
        public ValueTask<WorkflowMediaInspectionDecision> InspectAsync(Stream content, WorkflowMediaRecord media, CancellationToken cancellationToken)
        {
            Reference = media.Reference;
            return ValueTask.FromResult(WorkflowMediaInspectionDecision.Pending);
        }
    }
}

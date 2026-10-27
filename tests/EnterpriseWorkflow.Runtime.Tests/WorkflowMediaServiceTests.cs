using System.Reflection;
using System.Text;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Runtime;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class WorkflowMediaServiceTests
{
    [Fact]
    public async Task PrivateObjectsAreIdempotentAndSignatureValidationRejectsDisguisedContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-media-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = DispatchProxy.Create<IWorkflowStore, MediaStoreProxy>();
            var storage = new FileWorkflowMediaStorageProvider(root);
            var service = new WorkflowMediaService(store, storage, []);
            var scope = new WorkflowMediaWriteScope(new(new("local"), "alice"), null, null, "submission");
            using var text = new MemoryStream(Encoding.UTF8.GetBytes("Bonjour"));
            var staged = await service.StageAsync(scope, "document", "../../demande.txt", text, new(100, ["text/plain"], [".txt"]), new(false, []), TestContext.Current.CancellationToken);
            Assert.Equal("demande.txt", staged.Name);
            text.Position = 0;
            var replay = await service.StageAsync(scope, "document", "demande.txt", text, new(100, ["text/plain"], [".txt"]), new(false, []), TestContext.Current.CancellationToken);
            Assert.Equal(staged.Reference, replay.Reference);
            Assert.Single(Directory.GetFiles(root));
            await using var opened = await storage.OpenAsync(staged.ObjectKey, TestContext.Current.CancellationToken);
            using var reader = new StreamReader(opened);
            Assert.Equal("Bonjour", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
            text.Position = 0;
            await Assert.ThrowsAsync<InvalidDataException>(async () => await service.StageAsync(scope, "fake-pdf", "fake.pdf", text,
                new(100, ["application/pdf"], [".pdf"]), new(false, []), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(async () => await storage.OpenAsync("../escape", TestContext.Current.CancellationToken));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // DispatchProxy boxes the ValueTask; the service awaits it exactly once.
#pragma warning disable CA2012
    public class MediaStoreProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IWorkflowStore.StageMediaAsync) => ValueTask.FromResult(StoreResults.Succeeded(((StageWorkflowMediaCommand)args![0]!).Media)),
            _ => throw new NotSupportedException(),
        };
    }
}

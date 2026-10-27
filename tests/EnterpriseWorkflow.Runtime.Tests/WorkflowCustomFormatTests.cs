using System.Reflection;
using System.Text;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class WorkflowCustomFormatTests
{
    [Fact]
    public async Task StagingUsesCustomValidatorAndRejectsUnavailableOrIncorrectBindings()
    {
        var root = Path.Combine(Path.GetTempPath(), "custom-format-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = DispatchProxy.Create<IWorkflowStore, WorkflowMediaServiceTests.MediaStoreProxy>();
            var artifact = new WorkflowArtifactReference(new("module"), "1.0.0", new string('a', 64));
            var format = new WorkflowMediaFormatDefinition("module.note", "application/x-note", [".note"]);
            var specification = new WorkflowMediaSpecification(100, [format.ContentType], format.Extensions, [format]);
            var scope = new WorkflowMediaWriteScope(new(new("local"), "alice"), null, null, "submission", Artifact: artifact);
            var media = new WorkflowMediaService(store, new FileWorkflowMediaStorageProvider(root), [new ReadOnlyScanner()], formatResolver: new Resolver(artifact));
            using var accepted = new MemoryStream(Encoding.UTF8.GetBytes("NOTE:accepted"));
            var staged = await media.StageAsync(scope, "note", "file.note", accepted, specification, new(false, []), TestContext.Current.CancellationToken);
            Assert.Equal(format.ContentType, staged.ContentType);
            accepted.Position = 0;
            await Assert.ThrowsAsync<InvalidDataException>(async () => await media.StageAsync(scope with { Artifact = artifact with { Sha256 = new string('b', 64) } }, "wrong", "file.note", accepted, specification, new(false, []), TestContext.Current.CancellationToken));
            accepted.Position = 0;
            var unavailable = new WorkflowMediaService(store, new FileWorkflowMediaStorageProvider(root), []);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await unavailable.StageAsync(scope, "unavailable", "file.note", accepted, specification, new(false, []), TestContext.Current.CancellationToken));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void FormatsAreDeclaredAtTheModuleAndReferencedByPolicies()
    {
        var module = new WorkflowModuleBuilder(new("module", "1.0.0", 1));
        module.MediaFormats(formats => formats.Add<Validator>("note").ContentType("application/x-note").Extensions(".note"))
            .MediaPolicies(policies => policies.Add("notes").AllowFormats("note").MaximumSize(100).VisibleToInstanceReaders());
        var registry = new EnterpriseWorkflow.Presentation.WorkflowPresentationRegistryBuilder(); module.ConfigurePresentation(registry, []);
        Assert.Equal("module.note", Assert.Single(registry.MediaFormats).Definition.Id);
        var invalid = new WorkflowModuleBuilder(new("invalid", "1.0.0", 1));
        invalid.MediaPolicies(policies => policies.Add("notes").AllowFormats("missing").MaximumSize(100).VisibleToInstanceReaders());
        Assert.Throws<InvalidOperationException>(() => invalid.ConfigurePresentation(new(), []));
        Assert.Throws<InvalidOperationException>(() => module.MediaFormats(formats => formats.Add<Validator>("pdf")));
    }
    private sealed class ReadOnlyScanner : IWorkflowMediaScanner
    {
        public ValueTask<bool> ApproveAsync(Stream content, string detectedContentType, CancellationToken cancellationToken)
        {
            Assert.False(content.CanWrite);
            return ValueTask.FromResult(true);
        }
    }
    private sealed class Resolver(WorkflowArtifactReference expected) : IWorkflowMediaFormatResolver
    {
        public async ValueTask<bool> ValidateAsync(WorkflowArtifactReference artifact, WorkflowMediaFormatDefinition format, Stream content, CancellationToken cancellationToken) =>
            artifact == expected && await new Validator().ValidateAsync(content, cancellationToken);
    }
    private sealed class Validator : IWorkflowMediaFormatValidator
    {
        public async ValueTask<bool> ValidateAsync(Stream content, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(content, leaveOpen: true);
            return (await reader.ReadToEndAsync(cancellationToken)).StartsWith("NOTE:", StringComparison.Ordinal);
        }
    }
}

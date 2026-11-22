using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Extensions.Tests;

public sealed class RuntimeCompatibilityTests
{
    [Fact]
    public async Task ManifestRequirementAffectsIdentityButAbsentFieldPreservesHistoricalHash()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var assembly = Path.Combine(root, "module.dll");
            await File.WriteAllTextAsync(assembly, "manifest inspection does not execute module bytes", TestContext.Current.CancellationToken);
            var manifest = Path.Combine(root, "module.json");
            var json = WorkflowModuleManifestWriter.Create(new TestModule(), assembly);
            await File.WriteAllTextAsync(manifest, json, TestContext.Current.CancellationToken);
            var initial = WorkflowModuleCatalog.InspectArtifact(root);
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject(); node.Remove("runtime");
            await File.WriteAllTextAsync(manifest, node.ToJsonString(), TestContext.Current.CancellationToken);
            Assert.Equal(initial.Sha256, WorkflowModuleCatalog.InspectArtifact(root).Sha256);
            node["runtime"] = System.Text.Json.Nodes.JsonNode.Parse("""{"id":"Example.Workflow","versionRange":"[1.0.0,2.0.0)"}""");
            await File.WriteAllTextAsync(manifest, node.ToJsonString(), TestContext.Current.CancellationToken);
            var updated = WorkflowModuleCatalog.InspectArtifact(root);
            Assert.NotEqual(initial.Sha256, updated.Sha256);
            Assert.Equal("Example.Workflow", updated.Manifest.RuntimeRequirement!.Id);
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class TestModule : WorkflowModule
    {
        public override WorkflowModuleInfo Info => new("test.runtime", "1.0.0", 1);
        protected override void Configure(WorkflowModuleBuilder builder) { }
    }

    [Theory]
    [InlineData("2.3.0", true)]
    [InlineData("2.4.1", true)]
    [InlineData("2.2.9", false)]
    [InlineData("3.0.0", false)]
    [InlineData("2.3.0-beta.1", false)]
    public void WholeRuntimeRangeUsesNuGetSemantics(string version, bool compatible)
    {
        var options = new WorkflowModuleLoadOptions { RuntimeId = "Example.Workflow", RuntimeVersion = version };
        var requirement = new WorkflowRuntimeRequirement("Example.Workflow", "[2.3.0,3.0.0)");
        if (compatible) WorkflowRuntimeCompatibility.Validate(requirement, options);
        else Assert.Equal("EW7055_INCOMPATIBLE_RUNTIME", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowRuntimeCompatibility.Validate(requirement, options)).Code);
    }
    [Fact]
    public void RuntimeIdentityAndHistoricalPolicyAreIndependentFromVersion()
    {
        var options = new WorkflowModuleLoadOptions { RuntimeId = "Other.Workflow", RuntimeVersion = "2.4.1" };
        Assert.Throws<WorkflowModuleLoadException>(() => WorkflowRuntimeCompatibility.Validate(new("Example.Workflow", "[2.3.0,3.0.0)"), options));
        WorkflowRuntimeCompatibility.Validate(null, options);
        options.RequireRuntimeDeclaration = true;
        Assert.Throws<WorkflowModuleLoadException>(() => WorkflowRuntimeCompatibility.Validate(null, options));
        Assert.Equal("EW7054_INVALID_RUNTIME_REQUIREMENT", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowRuntimeCompatibility.Validate(new("Other.Workflow", "nonsense"), options)).Code);
    }
}

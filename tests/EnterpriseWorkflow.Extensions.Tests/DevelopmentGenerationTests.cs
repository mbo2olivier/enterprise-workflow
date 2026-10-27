using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Sdk;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using Xunit;
namespace EnterpriseWorkflow.Extensions.Tests;

public sealed class DevelopmentGenerationTests
{
    [Fact]
    public void DevelopmentIdentityIsExplicitAndProductionLoadingRefusesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-dev-identity-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var entry = Path.Combine(root, "module.dll"); File.Copy(typeof(FixtureModule).Assembly.Location, entry);
            File.WriteAllText(Path.Combine(root, "module.json"), WorkflowModuleManifestWriter.Create(new FixtureModule(), entry, developmentGeneration: 7));
            var error = Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleCatalog.InspectArtifact(root));
            Assert.Equal("EW7051_DEVELOPMENT_ARTIFACT_FORBIDDEN", error.Code);
            var inspected = WorkflowModuleCatalog.InspectArtifact(root, true);
            Assert.Equal(7, inspected.Manifest.DevelopmentGeneration); Assert.Equal("1.0.0-ewdev.g7", inspected.Manifest.Version);
            Assert.Equal("1.0.0", new FixtureModule().Info.ReleaseVersion);
        }
        finally { Directory.Delete(root, true); }
    }
    public sealed class FixtureModule : WorkflowModule
    {
        public override WorkflowModuleInfo Info { get; } = new("development.fixture", "1.0.0", 1);
        protected override void Configure(WorkflowModuleBuilder builder) { }
    }
}

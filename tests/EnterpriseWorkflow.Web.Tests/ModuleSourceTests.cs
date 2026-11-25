using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class ModuleSourceTests
{
    [Fact]
    public void DiscoveryIgnoresEvenMalformedIndividualPathsAndResolvesAgainstContentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "modules"));
        try
        {
            var configuration = Config(new() { ["Modules:RootDirectory"] = "modules", ["Modules:Directories"] = "not-an-array" });
            var diagnostics = new List<WorkflowModuleDiscoveryDiagnostic>();
            var catalog = WorkflowModuleSource.Load(configuration, root, reportDiagnostic: diagnostics.Add);
            Assert.Empty(catalog.Modules);
            Assert.Equal("EW7064_MODULE_DISCOVERY_SELECTED", Assert.Single(diagnostics).Code);
            configuration["Modules:RootDirectory"] = "missing";
            Assert.Equal("EW7060_INVALID_DISCOVERY_ROOT", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleSource.Load(configuration, root)).Code);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ExplicitEmptyDiscoveryRootNeverFallsBack(string? root)
    {
        var configuration = Config(new() { ["Modules:RootDirectory"] = root });
        Assert.Equal("EW7060_INVALID_DISCOVERY_ROOT", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleSource.Load(configuration, AppContext.BaseDirectory)).Code);
    }

    [Fact]
    public void LegacyListIsUsedOnlyWhenDiscoveryIsAbsent()
    {
        var diagnostics = new List<WorkflowModuleDiscoveryDiagnostic>();
        var configuration = Config(new());
        Assert.Empty(WorkflowModuleSource.Load(configuration, AppContext.BaseDirectory, reportDiagnostic: diagnostics.Add).Modules);
        Assert.Equal("EW7065_EXPLICIT_MODULES_SELECTED", Assert.Single(diagnostics).Code);
        configuration["Modules:Directories:0"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Equal("EW7001_MODULE_DIRECTORY_NOT_FOUND", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleSource.Load(configuration, AppContext.BaseDirectory)).Code);
    }

    [Fact]
    public async Task HeadlessHostingUsesDiscoveryWithoutTouchingIgnoredPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-discovery-host-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var builder = EnterpriseWorkflowApplication.CreateBuilder([]);
            builder.Configuration["Modules:RootDirectory"] = root;
            builder.Configuration["Modules:Directories:0"] = Path.Combine(root, "invalid-ignored");
            builder.Configuration["ConnectionStrings:EnterpriseWorkflow"] = $"Data Source={Path.Combine(root, "host.db")};Pooling=False";
            builder.ConfigureDiscovery(options => options.MaximumDepth = 0);
            await builder.MigrateAsync();
            await using var host = await builder.BuildAsync();
        }
        finally { Directory.Delete(root, true); }
    }
    private static IConfigurationRoot Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}

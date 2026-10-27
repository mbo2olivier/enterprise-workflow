using Xunit;

namespace EnterpriseWorkflow.Cli.Tests;

public sealed class DevelopmentConfigurationTests
{
    [Fact]
    public async Task NamedEnvironmentsMergeSettingsAndCliOverridesWithoutUsingTheLegacyWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-environment-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "ew.dev.json"), """{"schemaVersion":2,"defaultEnvironment":"rh","build":{"configuration":"Release","watch":true},"server":{"url":"http://localhost:5090"},"environments":{"rh":{"server":{"url":"http://localhost:5091"},"build":{"watch":false}},"other":{"server":{"url":"http://localhost:5092"}}}}""", TestContext.Current.CancellationToken);
            var project = Path.Combine(root, "module.csproj");
            var selected = await DevelopmentConfiguration.ReadAsync(project, null, null, false);
            Assert.Equal("rh", selected.EnvironmentName); Assert.Equal("http://localhost:5091", selected.Server.Url); Assert.False(selected.Watch);
            Assert.Equal("Release", selected.Build.Configuration);
            Assert.Equal(Path.Combine(root, ".ew", "environments", "rh", "workflow.db"), selected.Storage.Path);
            var other = await DevelopmentConfiguration.ReadAsync(project, null, "http://localhost:5093", true, "other", "Debug");
            Assert.Equal("http://localhost:5093", other.Server.Url); Assert.Equal("Debug", other.Build.Configuration); Assert.False(other.Watch);
            Assert.NotEqual(other.StateRoot, selected.StateRoot);
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentConfiguration.ReadAsync(project, null, null, false, "../escape"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ConfigurationUsesFileRelativePathsAndCliOverridesAndRejectsRemoteOrUnknownSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var project = Path.Combine(root, "module.csproj");
            var config = Path.Combine(root, "ew.dev.json");
            await File.WriteAllTextAsync(config, """{"storage":{"path":"data/test.db"},"watch":true}""", TestContext.Current.CancellationToken);
            var value = await DevelopmentConfiguration.ReadAsync(project, null, "http://127.0.0.1:5090", true);
            Assert.Equal(Path.Combine(root, "data", "test.db"), value.Storage.Path);
            Assert.False(value.Watch);
            Assert.Equal("http://127.0.0.1:5090", value.Server.Url);
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentConfiguration.ReadAsync(project, null, "http://0.0.0.0:5080", false));
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentConfiguration.ReadAsync(project, null, "http://localhost:5080/other", false));
            await File.WriteAllTextAsync(config, """{"unknown":true}""", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => DevelopmentConfiguration.ReadAsync(project, null, null, false));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InvalidSeedAndMissingExplicitConfigAreRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var project = Path.Combine(root, "module.csproj");
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentConfiguration.ReadAsync(project, Path.Combine(root, "missing.json"), null, false));
            await File.WriteAllTextAsync(Path.Combine(root, "ew.dev.json"), """{"seed":{"users":[{"id":"alice"},{"id":"alice"}]}}""", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentConfiguration.ReadAsync(project, null, null, false));
            await File.WriteAllTextAsync(Path.Combine(root, "ew.dev.json"), """{"seed":{"users":[{"id":"Alice"},{"id":"alice"}]}}""", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentConfiguration.ReadAsync(project, null, null, false));
        }
        finally { Directory.Delete(root, true); }
    }
}

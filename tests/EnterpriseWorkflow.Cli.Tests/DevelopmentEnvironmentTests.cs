using System.Text.Json;
using Xunit;

namespace EnterpriseWorkflow.Cli.Tests;

public sealed class DevelopmentEnvironmentTests
{
    [Fact]
    public async Task FileIsDataAndSecretsStayOutsideTheRuntimeSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, ".env"), """
                # Data, never evaluated by a shell
                Notifications__Smtp__Password='$(do-not-execute) secret'
                Module__ApiKey=private-module-key
                Development__Server__Url=http://localhost:5097
                Security__AuthenticationProvider=unsupported-in-dev
                """, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, ".env.other"), "Development__Server__Url=http://localhost:5999", TestContext.Current.CancellationToken);
            var values = await DevelopmentEnvironment.ReadAsync(root, null, false);
            var config = await DevelopmentConfiguration.ReadAsync(Path.Combine(root, "app.csproj"), null, null, false, "other", environmentValues: values);
            Assert.Equal("http://localhost:5097", config.Server.Url);
            Assert.Null(config.Smtp.Password);
            Assert.Equal("private-module-key", DevelopmentEnvironment.HostValues(values)["Module__ApiKey"]);
            Assert.DoesNotContain("private-module-key", JsonSerializer.Serialize(config), StringComparison.Ordinal);
            Assert.DoesNotContain("do-not-execute", JsonSerializer.Serialize(config), StringComparison.Ordinal);
            Assert.Equal("$(do-not-execute) secret", DevelopmentEnvironment.HostValues(values)["Notifications__Smtp__Password"]);
            Assert.DoesNotContain("Security__AuthenticationProvider", DevelopmentEnvironment.HostValues(values).Keys);
            var cli = await DevelopmentConfiguration.ReadAsync(Path.Combine(root, "app.csproj"), null, "http://localhost:5098", false, environmentValues: values);
            Assert.Equal("http://localhost:5098", cli.Server.Url);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExplicitMissingDisabledAndMalformedFilesAreHandledWithoutRevealingValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentEnvironment.ReadAsync(root, "missing.env", false));
            await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentEnvironment.ReadAsync(root, "file.env", true));
            await File.WriteAllTextAsync(Path.Combine(root, ".env"), "SECRET=top-secret\nSECRET=another-secret", TestContext.Current.CancellationToken);
            var error = await Assert.ThrowsAsync<ArgumentException>(() => DevelopmentEnvironment.ReadAsync(root, null, false));
            Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
            await DevelopmentEnvironment.ReadAsync(root, null, true);
        }
        finally { Directory.Delete(root, true); }
    }
}

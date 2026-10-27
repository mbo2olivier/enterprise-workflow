using Xunit;
namespace EnterpriseWorkflow.Cli.Tests;
public sealed class DevelopmentArtifactsTests
{
    [Fact]
    public void InvalidRegistryAndEnvironmentNamesAreRejectedWithoutSelectingAnotherGeneration()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-generation-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            Assert.Empty(DevelopmentArtifacts.ReadGenerations(root));
            File.WriteAllText(Path.Combine(root, "generations.json"), """[{"Number":-1,"Fingerprint":"invalid"}]""");
            Assert.Throws<InvalidOperationException>(() => DevelopmentArtifacts.ReadGenerations(root));
            Assert.Throws<ArgumentException>(() => DevelopmentConfiguration.ValidateEnvironment("../other"));
            Assert.Throws<ArgumentException>(() => DevelopmentConfiguration.ValidateEnvironment("CON"));
            DevelopmentConfiguration.ValidateEnvironment("recette-rh");
        }
        finally { Directory.Delete(root, true); }
    }
}

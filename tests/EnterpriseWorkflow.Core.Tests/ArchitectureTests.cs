using System.Xml.Linq;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class ArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ProductionProjectsHaveOnlyAllowedProjectReferences()
    {
        var expectedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["EnterpriseWorkflow.Abstractions"] = [],
            ["EnterpriseWorkflow.Core"] = ["EnterpriseWorkflow.Abstractions"],
            ["EnterpriseWorkflow.Persistence.Abstractions"] = ["EnterpriseWorkflow.Abstractions", "EnterpriseWorkflow.Core"],
            ["EnterpriseWorkflow.Sdk"] = ["EnterpriseWorkflow.Abstractions", "EnterpriseWorkflow.Core"],
        };

        var projectPaths = Directory.GetFiles(
            Path.Combine(RepositoryRoot, "src"),
            "*.csproj",
            SearchOption.AllDirectories);

        Assert.Equal(expectedReferences.Count, projectPaths.Length);

        foreach (var projectPath in projectPaths)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            Assert.True(expectedReferences.TryGetValue(projectName, out var expected), $"Unexpected production project: {projectName}");

            var projectDirectory = Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException($"Project has no parent directory: {projectPath}");
            var actual = XDocument.Load(projectPath)
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(include => include is not null)
                .Select(include => Path.GetFullPath(include!, projectDirectory))
                .Select(Path.GetFileNameWithoutExtension)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected!.Order(StringComparer.Ordinal), actual);
        }
    }

    [Fact]
    public void ProductionProjectsDoNotReferenceExternalPackages()
    {
        var packageReferences = Directory.GetFiles(
                Path.Combine(RepositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .SelectMany(projectPath => XDocument.Load(projectPath).Descendants("PackageReference"))
            .ToArray();

        Assert.Empty(packageReferences);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EnterpriseWorkflow.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}

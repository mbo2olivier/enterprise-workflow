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
            ["EnterpriseWorkflow.Extensions.Abstractions"] = ["EnterpriseWorkflow.Runtime"],
            ["EnterpriseWorkflow.Extensions"] = ["EnterpriseWorkflow.Extensions.Abstractions", "EnterpriseWorkflow.Runtime"],
            ["EnterpriseWorkflow.Persistence.Abstractions"] = ["EnterpriseWorkflow.Abstractions", "EnterpriseWorkflow.Core"],
            ["EnterpriseWorkflow.Persistence.Oracle"] = ["EnterpriseWorkflow.Persistence.Abstractions"],
            ["EnterpriseWorkflow.Persistence.Sqlite"] = ["EnterpriseWorkflow.Persistence.Abstractions"],
            ["EnterpriseWorkflow.Runtime.Abstractions"] =
                ["EnterpriseWorkflow.Abstractions", "EnterpriseWorkflow.Core", "EnterpriseWorkflow.Persistence.Abstractions"],
            ["EnterpriseWorkflow.Runtime"] =
                ["EnterpriseWorkflow.Core", "EnterpriseWorkflow.Runtime.Abstractions", "EnterpriseWorkflow.Security.Abstractions"],
            ["EnterpriseWorkflow.Security.Abstractions"] = [],
            ["EnterpriseWorkflow.Security"] = ["EnterpriseWorkflow.Security.Abstractions"],
            ["EnterpriseWorkflow.Security.Persistence"] = ["EnterpriseWorkflow.Security.Abstractions"],
            ["EnterpriseWorkflow.Security.Persistence.Oracle"] = ["EnterpriseWorkflow.Security.Persistence"],
            ["EnterpriseWorkflow.Security.Persistence.Sqlite"] = ["EnterpriseWorkflow.Security.Persistence"],
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
    public void OnlyProviderProjectsReferenceApprovedExternalPackages()
    {
        var packageReferences = Directory.GetFiles(
                Path.Combine(RepositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .SelectMany(projectPath => XDocument.Load(projectPath)
                .Descendants("PackageReference")
                .Select(reference => new
                {
                    Project = Path.GetFileNameWithoutExtension(projectPath),
                    Package = reference.Attribute("Include")?.Value,
                }))
            .ToArray();

        Assert.Equal(
            [
                (Project: "EnterpriseWorkflow.Extensions.Abstractions", Package: "Microsoft.Extensions.DependencyInjection.Abstractions"),
                (Project: "EnterpriseWorkflow.Persistence.Oracle", Package: "Oracle.EntityFrameworkCore"),
                (Project: "EnterpriseWorkflow.Persistence.Sqlite", Package: "Microsoft.EntityFrameworkCore.Sqlite"),
                (Project: "EnterpriseWorkflow.Runtime", Package: "Microsoft.Extensions.DependencyInjection.Abstractions"),
                (Project: "EnterpriseWorkflow.Runtime", Package: "Microsoft.Extensions.Hosting.Abstractions"),
                (Project: "EnterpriseWorkflow.Runtime", Package: "Microsoft.Extensions.Options"),
                (Project: "EnterpriseWorkflow.Security", Package: "System.DirectoryServices.Protocols"),
                (Project: "EnterpriseWorkflow.Security.Persistence", Package: "Microsoft.EntityFrameworkCore"),
                (Project: "EnterpriseWorkflow.Security.Persistence", Package: "Microsoft.EntityFrameworkCore.Relational"),
                (Project: "EnterpriseWorkflow.Security.Persistence.Oracle", Package: "Oracle.EntityFrameworkCore"),
                (Project: "EnterpriseWorkflow.Security.Persistence.Sqlite", Package: "Microsoft.EntityFrameworkCore.Sqlite"),
            ],
            packageReferences.Select(item => (item.Project, item.Package)).OrderBy(item => item.Project, StringComparer.Ordinal));
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

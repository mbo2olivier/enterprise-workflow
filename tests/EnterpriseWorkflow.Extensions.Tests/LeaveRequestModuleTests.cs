using EnterpriseWorkflow.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Extensions.Tests;

public sealed class LeaveRequestModuleTests
{
    [Fact]
    public async Task ExactArtifactDeclaresVersionedProcessFormComponentAndDesignatedApproval()
    {
        var root = Path.Combine(Path.GetTempPath(), $"enterprise-workflow-leave-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "dependencies"));
            var repository = FindRepositoryRoot();
            var source = Path.Combine(repository, "samples", "LeaveRequest.Module", "bin",
                BuildConfiguration(), "net10.0", "EnterpriseWorkflow.LeaveRequest.Module.dll");
            var assembly = Path.Combine(root, "EnterpriseWorkflow.LeaveRequest.Module.dll");
            File.Copy(source, assembly);
            var sdkSource = Path.Combine(repository, "src", "EnterpriseWorkflow.Sdk", "bin",
                BuildConfiguration(), "net10.0", "EnterpriseWorkflow.Sdk.dll");
            var sdk = Path.Combine(root, "dependencies", "EnterpriseWorkflow.Sdk.dll");
            File.Copy(sdkSource, sdk);
            var moduleType = System.Reflection.Assembly.LoadFrom(source).GetType("EnterpriseWorkflow.LeaveRequest.LeaveRequestModule", throwOnError: true)!;
            var authoringModule = (WorkflowModule)Activator.CreateInstance(moduleType)!;
            var manifest = WorkflowModuleManifestWriter.Create(authoringModule, assembly, [sdk]);
            await File.WriteAllTextAsync(Path.Combine(root, "module.json"), manifest, TestContext.Current.CancellationToken);

            var moduleCatalog = WorkflowModuleCatalog.Load([root]);
            var services = new ServiceCollection();
            services.AddEnterpriseWorkflowModules(moduleCatalog);
            await using var provider = services.BuildServiceProvider();
            var catalog = provider.GetRequiredService<IWorkflowPresentationCatalog>();

            var process = Assert.Single(catalog.Processes);
            Assert.Equal("sample.leave-request.leave-request", process.Definition.Id.Value);
            Assert.Empty(process.DesignatedAssignmentNodes);
            Assert.Equal(4, catalog.Permissions.Length);
            Assert.Contains(process.Definition.Nodes, node => node.Id.Value == "validation-manager");
            Assert.True(catalog.TryResolveForm(process.Definition, process.StartForm, out var form));
            Assert.NotNull(form!.CustomComponentType);
            Assert.Equal("LeaveRequestSummary", form.CustomComponentType.Name);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) when (OperatingSystem.IsWindows()) { }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows()) { }
        }
    }

    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
        ?? throw new InvalidOperationException("Could not determine the build configuration.");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EnterpriseWorkflow.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}

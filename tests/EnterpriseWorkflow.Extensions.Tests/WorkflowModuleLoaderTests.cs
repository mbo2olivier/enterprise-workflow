using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Runtime;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EnterpriseWorkflow.Extensions.Tests;

public sealed class WorkflowModuleLoaderTests
{
    [Fact]
    public async Task VersionsCoexistWithPrivateDependenciesHandlersComponentsAndResources()
    {
        await using var artifacts = await ModuleArtifacts.CreateAsync();
        var catalog = WorkflowModuleCatalog.Load([artifacts.VersionA, artifacts.VersionB]);

        Assert.Equal(2, catalog.Modules.Length);
        Assert.Equal("1.0.0", catalog.Modules[0].Manifest.Version);
        Assert.Equal("2.0.0", catalog.Modules[1].Manifest.Version);
        var contextA = Assert.IsAssignableFrom<AssemblyLoadContext>(AssemblyLoadContext.GetLoadContext(catalog.Modules[0].EntryAssembly));
        var contextB = Assert.IsAssignableFrom<AssemblyLoadContext>(AssemblyLoadContext.GetLoadContext(catalog.Modules[1].EntryAssembly));
        Assert.NotSame(AssemblyLoadContext.Default, contextA);
        Assert.NotSame(contextA, contextB);
        Assert.False(contextA.IsCollectible);
        Assert.False(contextB.IsCollectible);

        var services = new ServiceCollection();
        services.AddEnterpriseWorkflowModules(catalog);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowHandlerRegistry>();
        Assert.True(registry.TryGet(new(new TechnicalId("fixture.approval"), 1), out var handlerA));
        Assert.True(registry.TryGet(new(new TechnicalId("fixture.approval"), 2), out var handlerB));
        Assert.Same(contextA, AssemblyLoadContext.GetLoadContext(handlerA!.HandlerType.Assembly));
        Assert.Same(contextB, AssemblyLoadContext.GetLoadContext(handlerB!.HandlerType.Assembly));

        var htmlA = await RenderAsync(catalog.Modules[0].Components.Single().ComponentType);
        var htmlB = await RenderAsync(catalog.Modules[1].Components.Single().ComponentType);
        Assert.Contains("private-a", htmlA, StringComparison.Ordinal);
        Assert.Contains("private-b", htmlB, StringComparison.Ordinal);

        foreach (var module in catalog.Modules)
        {
            Assert.True(catalog.TryOpenResource(module.Manifest.Id.Value, module.Manifest.Version,
                module.ArtifactSha256, "resources/styles.css", out var resource, out var content));
            await using (content)
            using (var reader = new StreamReader(content!))
                Assert.Contains("approval-panel", await reader.ReadToEndAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            Assert.Equal("text/css", resource!.ContentType);
            Assert.False(catalog.TryOpenResource(module.Manifest.Id.Value, module.Manifest.Version,
                new string('0', 64), "resources/styles.css", out _, out _));
        }
    }

    [Fact]
    public async Task HandlerCollisionIsRejectedInsteadOfUsingRegistrationOrder()
    {
        await using var artifacts = await ModuleArtifacts.CreateAsync();
        var catalog = WorkflowModuleCatalog.Load([artifacts.VersionA]);
        var services = new ServiceCollection();

        var exception = Assert.Throws<WorkflowHandlerRegistrationException>(() =>
            services.AddEnterpriseWorkflowModules(catalog,
                handlers => handlers.AddHumanTask<HostCollisionHandler>("fixture.approval", 1)));

        Assert.Equal("EW4001_DUPLICATE_HANDLER", exception.Code);
    }

    [Fact]
    public async Task TamperedResourcePreventsModuleCodeFromLoading()
    {
        await using var artifacts = await ModuleArtifacts.CreateAsync();
        await File.AppendAllTextAsync(Path.Combine(artifacts.VersionA, "resources", "styles.css"), "tampered",
            TestContext.Current.CancellationToken);

        var exception = Assert.Throws<WorkflowModuleLoadException>(() =>
            WorkflowModuleCatalog.Load([artifacts.VersionA]));

        Assert.Equal("EW7006_MODULE_FILE_HASH_MISMATCH", exception.Code);
    }

    [Fact]
    public async Task KernelRestartReconcilesNewVersionAndRunsExactExplicitStateMigration()
    {
        await using var artifacts = await ModuleArtifacts.CreateAsync();
        var firstCatalog = WorkflowModuleCatalog.Load([artifacts.VersionA]);
        var restartedCatalog = WorkflowModuleCatalog.Load([artifacts.VersionA, artifacts.VersionB]);
        var artifact = new ModuleArtifactReference(firstCatalog.Modules[0].Manifest.Id,
            firstCatalog.Modules[0].Manifest.Version, firstCatalog.Modules[0].ArtifactSha256);
        var store = new MaintenanceStoreStub(artifact);

        Assert.Single((await WorkflowModuleKernel.ReconcileAsync(firstCatalog, store,
            TestContext.Current.CancellationToken)).InstalledArtifacts);
        Assert.Equal(2, (await WorkflowModuleKernel.ReconcileAsync(restartedCatalog, store,
            TestContext.Current.CancellationToken)).InstalledArtifacts.Count);

        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowMaintenanceStore>(store);
        services.AddEnterpriseWorkflowModules(restartedCatalog);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<WorkflowStateMigrationService>().MigrateAsync(
            store.InstanceId, artifact, 2, new ActorIdentity(new TechnicalId("local"), "operator"),
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, result.Outcome);
        Assert.Equal(2, result.Value!.SchemaVersion);
        Assert.Equal("{\"migrated\":true}", store.State.Value.CanonicalText);
        Assert.Equal(1, store.CommitCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<WorkflowStateMigrationService>().MigrateAsync(
                store.InstanceId, artifact, 3, new ActorIdentity(new TechnicalId("local"), "operator"),
                TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(2, store.State.SchemaVersion);
        Assert.Equal(1, store.CommitCount);
    }

    private static async Task<string> RenderAsync(Type componentType)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync(componentType, ParameterView.Empty);
            return output.ToHtmlString();
        });
    }

    private sealed class HostCollisionHandler : IHumanTaskCompletionHandler
    {
        public ValueTask<HumanTaskCompletionResult> CompleteAsync(
            HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(HumanTaskCompletionResult.Success(new TechnicalId("approved")));
    }

    private sealed class MaintenanceStoreStub(ModuleArtifactReference artifact) : IWorkflowMaintenanceStore
    {
        public WorkflowInstanceId InstanceId { get; } = new(Guid.NewGuid());
        public WorkflowState State { get; private set; } = WorkflowState.Create("{\"value\":1}", 1);
        public int CommitCount { get; private set; }

        public ValueTask<StoreResult<ModuleArtifactReconciliationResult>> ReconcileModuleArtifactsAsync(
            ReconcileModuleArtifactsCommand command, CancellationToken cancellationToken) =>
            ValueTask.FromResult(StoreResults.Succeeded(new ModuleArtifactReconciliationResult(command.ConfiguredArtifacts)));

        public ValueTask<StoreResult<StateMigrationSnapshot>> ReadStateMigrationSnapshotAsync(
            WorkflowInstanceId instanceId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(StoreResults.Succeeded(new StateMigrationSnapshot(
                InstanceId, new DefinitionReference(new TechnicalId("fixture.workflow"), 1, new string('d', 64)),
                [artifact], WorkflowInstanceStatus.Waiting, CommitCount, State)));

        public ValueTask<StoreResult<StateMigrationResult>> CommitStateMigrationAsync(
            CommitStateMigrationCommand command, CancellationToken cancellationToken)
        {
            if (command.ExpectedRevision != CommitCount || command.SourceSchemaVersion != State.SchemaVersion)
                return ValueTask.FromResult(StoreResults.Conflict<StateMigrationResult>("EW3054_STALE_STATE_MIGRATION"));
            State = command.ReplacementState;
            CommitCount++;
            return ValueTask.FromResult(StoreResults.Succeeded(new StateMigrationResult(CommitCount, State.SchemaVersion)));
        }
    }

    private sealed class ModuleArtifacts : IAsyncDisposable
    {
        private readonly string _root;

        private ModuleArtifacts(string root)
        {
            _root = root;
            VersionA = Path.Combine(root, "approval", "1.0.0");
            VersionB = Path.Combine(root, "approval", "2.0.0");
        }

        public string VersionA { get; }
        public string VersionB { get; }

        public static async Task<ModuleArtifacts> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), $"enterprise-workflow-modules-{Guid.NewGuid():N}");
            var artifacts = new ModuleArtifacts(root);
            await StageAsync("ApprovalModule.V1", "PrivateDependency.V1", artifacts.VersionA, "1.0.0");
            await StageAsync("ApprovalModule.V2", "PrivateDependency.V2", artifacts.VersionB, "2.0.0");
            return artifacts;
        }

        private static async Task StageAsync(string moduleProject, string dependencyProject, string destination, string version)
        {
            Directory.CreateDirectory(Path.Combine(destination, "dependencies"));
            Directory.CreateDirectory(Path.Combine(destination, "resources"));
            var repository = FindRepositoryRoot();
            var moduleOutput = FindFixtureOutput(repository, moduleProject,
                "EnterpriseWorkflow.Fixtures.ApprovalModule.dll");
            var dependencyOutput = FindFixtureOutput(repository, dependencyProject,
                "EnterpriseWorkflow.Fixtures.PrivateDependency.dll");
            var entryPath = Path.Combine(destination, "EnterpriseWorkflow.Fixtures.ApprovalModule.dll");
            var dependencyPath = Path.Combine(destination, "dependencies", "EnterpriseWorkflow.Fixtures.PrivateDependency.dll");
            var resourcePath = Path.Combine(destination, "resources", "styles.css");
            File.Copy(Path.Combine(moduleOutput, "EnterpriseWorkflow.Fixtures.ApprovalModule.dll"), entryPath);
            File.Copy(Path.Combine(dependencyOutput, "EnterpriseWorkflow.Fixtures.PrivateDependency.dll"), dependencyPath);
            File.Copy(Path.Combine(repository, "tests", "Fixtures", moduleProject, "resources", "styles.css"), resourcePath);
            var manifest = new
            {
                schemaVersion = 1,
                id = "fixture.approval",
                version,
                contractVersion = 1,
                entryAssembly = "EnterpriseWorkflow.Fixtures.ApprovalModule.dll",
                entryType = "EnterpriseWorkflow.Fixtures.ApprovalModule.ApprovalModule",
                entryAssemblySha256 = Hash(entryPath),
                dependencies = new[] { new { path = "dependencies/EnterpriseWorkflow.Fixtures.PrivateDependency.dll", sha256 = Hash(dependencyPath) } },
                resources = new[] { new { path = "resources/styles.css", contentType = "text/css", sha256 = Hash(resourcePath) } },
                components = new[] { new { id = "fixture.approval-panel", type = "EnterpriseWorkflow.Fixtures.ApprovalModule.ApprovalPanel" } },
            };
            await File.WriteAllTextAsync(Path.Combine(destination, "module.json"), JsonSerializer.Serialize(manifest),
                TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (!Directory.Exists(_root)) return ValueTask.CompletedTask;

            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException) when (OperatingSystem.IsWindows())
            {
                // Non-collectible AssemblyLoadContexts retain loaded DLLs until process exit.
                // Windows locks those staged files; the runner removes its temporary tree later.
            }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
            {
                // Windows can report the same loaded-assembly lock as access denied.
            }
            return ValueTask.CompletedTask;
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        private static string FindFixtureOutput(
            string repository, string project, string expectedAssembly)
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                ?? throw new InvalidOperationException("Could not determine the current build configuration.");
            var output = new[] { configuration, "Debug", "Release" }.Distinct(StringComparer.Ordinal)
                .Select(candidate => Path.Combine(repository, "tests", "Fixtures", project, "bin", candidate, "net10.0"))
                .Where(candidate => File.Exists(Path.Combine(candidate, expectedAssembly)))
                .OrderByDescending(candidate => File.GetLastWriteTimeUtc(Path.Combine(candidate, expectedAssembly)))
                .FirstOrDefault();
            if (output is not null) return output;
            throw new FileNotFoundException($"Fixture output '{expectedAssembly}' was not built for '{project}'.");
        }

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
}

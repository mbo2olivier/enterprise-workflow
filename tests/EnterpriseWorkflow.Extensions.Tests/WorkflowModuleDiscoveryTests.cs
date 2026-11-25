using System.Text.Json.Nodes;
using EnterpriseWorkflow.Extensions;
using Xunit;

namespace EnterpriseWorkflow.Extensions.Tests;

public sealed class WorkflowModuleDiscoveryTests
{
    [Fact]
    public void ScanIsOrderedStopsAtArtifactsAndIgnoresNonModules()
    {
        using var tree = new Tree();
        var z = tree.Directory("z/2.0.0"); var a = tree.Directory("a/1.0.0");
        File.WriteAllText(Path.Combine(z, "module.json"), "{}");
        File.WriteAllText(Path.Combine(a, "module.json"), "{}");
        var nested = tree.Directory("a/1.0.0/dependencies/nested");
        File.WriteAllText(Path.Combine(nested, "module.json"), "invalid nested manifest must not be read");
        tree.Directory("non-module");
        var found = WorkflowModuleDiscovery.FindArtifacts(tree.Root);
        Assert.Equal(2, found.Count); Assert.Equal(a, found[0]); Assert.Equal(z, found[1]);
        Assert.Equal(a, Assert.Single(WorkflowModuleDiscovery.FindArtifacts(a)));
    }

    [Theory]
    [InlineData("depth")]
    [InlineData("directories")]
    [InlineData("candidates")]
    [InlineData("entries")]
    public void BoundsFailWithoutReturningAPartialCatalog(string bound)
    {
        using var tree = new Tree();
        foreach (var name in new[] { "a", "b" })
            File.WriteAllText(Path.Combine(tree.Directory(name), "module.json"), "{}");
        var options = new WorkflowModuleDiscoveryOptions();
        switch (bound)
        {
            case "depth": options.MaximumDepth = 0; break;
            case "directories": options.MaximumDirectories = 1; break;
            case "candidates": options.MaximumCandidates = 1; break;
            case "entries": options.MaximumEntries = 1; break;
        }
        Assert.Equal("EW7062_DISCOVERY_LIMIT_EXCEEDED", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleDiscovery.FindArtifacts(tree.Root, options)).Code);
    }

    [Fact]
    public void EmptyRootsAreValidButMissingRootsAndMalformedManifestsFail()
    {
        using var tree = new Tree();
        Assert.Empty(WorkflowModuleCatalog.Discover(tree.Root).Modules);
        Assert.Equal("EW7060_INVALID_DISCOVERY_ROOT", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleDiscovery.FindArtifacts(Path.Combine(tree.Root, "missing"))).Code);
        File.WriteAllText(Path.Combine(tree.Root, "module.json"), "invalid JSON");
        Assert.Equal("EW7003_INVALID_MODULE_MANIFEST", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleCatalog.Discover(tree.Root)).Code);
    }

    [Fact]
    public void SymbolicLinksAreSkippedAndCannotBeTheConfiguredRoot()
    {
        using var tree = new Tree(); using var outside = new Tree();
        File.WriteAllText(Path.Combine(outside.Root, "module.json"), "{}");
        var link = Path.Combine(tree.Root, "linked");
        try { System.IO.Directory.CreateSymbolicLink(link, outside.Root); }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows()) { Assert.Skip("Creating symbolic links requires Windows developer mode or privileges."); }
        var diagnostics = new List<WorkflowModuleDiscoveryDiagnostic>();
        Assert.Empty(WorkflowModuleDiscovery.FindArtifacts(tree.Root, new() { ReportDiagnostic = diagnostics.Add }));
        Assert.Equal("EW7063_DISCOVERY_LINK_SKIPPED", Assert.Single(diagnostics).Code);
        Assert.Equal("EW7060_INVALID_DISCOVERY_ROOT", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleDiscovery.FindArtifacts(link)).Code);
    }

    [Fact]
    public void InaccessibleDirectoriesFailRatherThanBeingSilentlyIgnored()
    {
        if (OperatingSystem.IsWindows()) { Assert.Skip("Unix permission test; Windows ACL coverage is in the heavy recipe."); return; }
        using var tree = new Tree(); var blocked = tree.Directory("blocked");
        var original = File.GetUnixFileMode(blocked);
        try
        {
            File.SetUnixFileMode(blocked, UnixFileMode.None);
            var privileged = false;
            try { _ = System.IO.Directory.GetFileSystemEntries(blocked); privileged = true; }
            catch (UnauthorizedAccessException) { }
            if (privileged) Assert.Skip("The account bypasses Unix directory permissions.");
            Assert.Equal("EW7061_DISCOVERY_SCAN_FAILED", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleDiscovery.FindArtifacts(tree.Root)).Code);
        }
        finally { File.SetUnixFileMode(blocked, original); }
    }

    [Fact]
    public async Task EligibleVersionsLoadAndIncompatibleCodeIsNotActivated()
    {
        await using var artifacts = await WorkflowModuleLoaderTests.ModuleArtifacts.CreateAsync();
        var catalog = WorkflowModuleCatalog.Discover(artifacts.Root);
        Assert.Equal(2, catalog.Modules.Length);
        var path = Path.Combine(artifacts.VersionB, "module.json");
        var json = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        json["runtime"] = JsonNode.Parse("""{"id":"Other.Runtime","versionRange":"[1.0.0,2.0.0)"}""");
        json["entryType"] = "Must.Not.Be.Activated";
        await File.WriteAllTextAsync(path, json.ToJsonString(), TestContext.Current.CancellationToken);
        var diagnostics = new List<WorkflowModuleDiscoveryDiagnostic>();
        var filtered = WorkflowModuleCatalog.Discover(artifacts.Root, configureDiscovery: options => options.ReportDiagnostic = diagnostics.Add);
        Assert.Equal("1.0.0", Assert.Single(filtered.Modules).Manifest.Version);
        Assert.Equal("EW7055_INCOMPATIBLE_RUNTIME", Assert.Single(diagnostics).Code);
        Assert.Equal("EW7055_INCOMPATIBLE_RUNTIME", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleCatalog.Load([artifacts.VersionB])).Code);
        // Incompatibility never excuses corrupted bytes.
        await File.AppendAllTextAsync(Path.Combine(artifacts.VersionB, "resources", "styles.css"), "tampered", TestContext.Current.CancellationToken);
        Assert.Equal("EW7006_MODULE_FILE_HASH_MISMATCH", Assert.Throws<WorkflowModuleLoadException>(() => WorkflowModuleCatalog.Discover(artifacts.Root)).Code);
    }

    [Fact]
    public async Task DuplicateVersionsFailEvenWhenEveryCandidateIsIncompatible()
    {
        await using var artifacts = await WorkflowModuleLoaderTests.ModuleArtifacts.CreateAsync();
        var path = Path.Combine(artifacts.VersionB, "module.json");
        var json = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        json["version"] = "1.0.0";
        await File.WriteAllTextAsync(path, json.ToJsonString(), TestContext.Current.CancellationToken);
        Assert.Equal("EW7012_DUPLICATE_MODULE_VERSION", Assert.Throws<WorkflowModuleLoadException>(() =>
            WorkflowModuleCatalog.Discover(artifacts.Root, options => options.RequireRuntimeDeclaration = true)).Code);
    }

    private sealed class Tree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ew-discovery-" + Guid.NewGuid().ToString("N"));
        public Tree() => System.IO.Directory.CreateDirectory(Root);
        public string Directory(string relative) { var path = Path.Combine(Root, relative); System.IO.Directory.CreateDirectory(path); return path; }
        public void Dispose() => System.IO.Directory.Delete(Root, recursive: true);
    }
}

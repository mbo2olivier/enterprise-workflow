using System.Security.Cryptography;
using System.Text.Json;

namespace EnterpriseWorkflow.Extensions;

/// <summary>Produces manifest identity and component declarations from the author's single metadata source.</summary>
public static class WorkflowModuleManifestWriter
{
    public static string Create(WorkflowModule entryPoint, string entryAssemblyPath,
        IEnumerable<string>? dependencyPaths = null,
        IEnumerable<(string Path, string ContentType)>? resources = null, int? developmentGeneration = null)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        if (developmentGeneration is null && entryPoint.Info.ContractRevision >= 1000000000) throw new InvalidOperationException("Contract revisions from 1000000000 are reserved for local development generations.");
        if (developmentGeneration is { } generation) entryPoint.SetDevelopmentGeneration(generation);
        var metadata = ((IWorkflowModule)entryPoint).Info!;
        var fullPath = Path.GetFullPath(entryAssemblyPath);
        var root = Path.GetDirectoryName(fullPath)!;
        string Relative(string path)
        {
            var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
            if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                throw new ArgumentException("Manifest files must be inside the package directory.");
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
        return JsonSerializer.Serialize(new
        {
            developmentGeneration,
            schemaVersion = WorkflowModuleLoadOptions.CurrentManifestSchemaVersion,
            id = metadata.Id.Value,
            version = metadata.ReleaseVersion,
            contractVersion = WorkflowModuleLoadOptions.CurrentContractVersion,
            entryAssembly = Relative(fullPath),
            entryType = entryPoint.GetType().FullName,
            entryAssemblySha256 = Hash(fullPath),
            dependencies = (dependencyPaths ?? []).Order(StringComparer.Ordinal).Select(path => new { path = Relative(path), sha256 = Hash(path) }),
            resources = (resources ?? []).OrderBy(item => item.Path, StringComparer.Ordinal).Select(item => new { path = Relative(item.Path), contentType = item.ContentType, sha256 = Hash(item.Path) }),
            components = entryPoint.Components.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new { id = item.Key, type = item.Value.FullName }),
        });
    }
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}

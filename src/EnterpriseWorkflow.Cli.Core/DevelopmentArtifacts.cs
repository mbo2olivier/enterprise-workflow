using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence.Sqlite;

internal sealed record DevelopmentGeneration(int Number, string Fingerprint);
internal static class DevelopmentArtifacts
{
    internal static string Fingerprint(string directory)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "module.json")));
        var root = document.RootElement;
        var values = new List<string> { root.GetProperty("id").GetString()!, root.GetProperty("entryAssemblySha256").GetString()! };
        foreach (var property in new[] { "dependencies", "resources", "components" }) values.Add(root.GetProperty(property).GetRawText());
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
    }
    internal static List<DevelopmentGeneration> ReadGenerations(string stateRoot)
    {
        var path = Path.Combine(stateRoot, "generations.json");
        if (!File.Exists(path)) return [];
        var generations = JsonSerializer.Deserialize<List<DevelopmentGeneration>>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Invalid generation registry.");
        if (generations.Any(item => item.Number is < 1 or > 1000000000 || item.Fingerprint is not { Length: 64 } || !item.Fingerprint.All(char.IsAsciiHexDigit)) ||
            generations.Select(item => item.Number).Distinct().Count() != generations.Count)
            throw new InvalidOperationException("Invalid generation registry.");
        return generations;
    }
    internal static string CacheArtifact(string stateRoot, string source, bool allowDevelopment)
    {
        source = Path.GetFullPath(source);
        var inspected = WorkflowModuleCatalog.InspectArtifact(source, allowDevelopment);
        var target = Path.Combine(stateRoot, "artifacts", "dependencies", inspected.Manifest.Id.Value, inspected.Manifest.Version, inspected.Sha256);
        EnsureOwnedPath(target);
        if (Directory.Exists(target))
        {
            if (WorkflowModuleCatalog.InspectArtifact(target, allowDevelopment).Sha256 != inspected.Sha256) throw new InvalidOperationException("Cached artifact content changed.");
            return target;
        }
        var staging = Path.Combine(stateRoot, "import-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        try
        {
            var paths = inspected.Manifest.Dependencies.Select(item => item.Path).Concat(inspected.Manifest.Resources.Select(item => item.Path))
                .Append(inspected.Manifest.EntryAssembly).Append("module.json").Distinct(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var destination = Path.Combine(staging, path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(Path.Combine(source, path), destination);
            }
            if (WorkflowModuleCatalog.InspectArtifact(staging, allowDevelopment).Sha256 != inspected.Sha256) throw new InvalidOperationException("Artifact changed while copying.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); Directory.Move(staging, target); return target;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    internal static async Task<string[]> RequiredAsync(string stateRoot, string databasePath, string primary, string[] additional, bool allowDevelopment)
    {
        var database = new SqliteWorkflowDatabase(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        await database.MigrateAsync(CancellationToken.None);
        var requirements = await new SqliteWorkflowStore(database).ReadDevelopmentRequirementsAsync(CancellationToken.None);
        var selected = new List<string> { Path.GetFullPath(primary) };
        selected.AddRange(additional.Select(Path.GetFullPath));
        foreach (var requirement in requirements)
        {
            var wanted = requirement.Artifact;
            var found = selected.FirstOrDefault(path => Matches(path));
            if (found is null)
            {
                var cache = Path.Combine(stateRoot, "artifacts");
                if (Directory.Exists(cache)) found = Directory.GetFiles(cache, "module.json", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true })
                    .Select(path => Path.GetDirectoryName(path)!).FirstOrDefault(path => Matches(path));
            }
            if (found is null) throw new InvalidOperationException($"Required artifact {wanted.Id.Value}/{wanted.Version}/{wanted.Sha256} is unavailable in environment {stateRoot}. Instances: {string.Join(", ", requirement.Instances.Take(10))}. No release was substituted.");
            selected.Add(found);
            bool Matches(string path)
            {
                try
                {
                    var manifestPath = Path.Combine(path, "module.json");
                    if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 256 * 1024) return false;
                    // Read metadata without activating unused historical assemblies.
                    using var json = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    if (!json.RootElement.TryGetProperty("id", out var id) || !json.RootElement.TryGetProperty("version", out var version) ||
                        id.ValueKind != JsonValueKind.String || version.ValueKind != JsonValueKind.String || id.GetString() != wanted.Id.Value || version.GetString() != wanted.Version) return false;
                    var inspected = WorkflowModuleCatalog.InspectArtifact(path, allowDevelopment);
                    return inspected.Sha256 == wanted.Sha256;
                }
                catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or WorkflowModuleLoadException) { return false; }
            }
        }
        return selected.Distinct(StringComparer.Ordinal).ToArray();
    }
    internal static async Task<string> DescribeRequirementsAsync(string databasePath)
    {
        var database = new SqliteWorkflowDatabase(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        var requirements = await new SqliteWorkflowStore(database).ReadDevelopmentRequirementsAsync(CancellationToken.None);
        return string.Join("; ", requirements.Take(10).Select(item => $"{item.Artifact.Id.Value}/{item.Artifact.Version}/{item.Artifact.Sha256}: {string.Join(", ", item.Instances.Take(10))}"));
    }

    internal static void EnsureOwnedPath(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new ArgumentException("Development environment directories must not traverse symbolic links.");
            current = current.Parent;
        }
    }
}

using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Extensions;

internal static partial class WorkflowModuleManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly HashSet<string> AllowedResourceContentTypes = new(StringComparer.Ordinal)
    {
        "application/javascript",
        "application/json",
        "font/woff2",
        "image/gif",
        "image/jpeg",
        "image/png",
        "image/svg+xml",
        "image/webp",
        "text/css",
        "text/javascript",
        "text/plain",
    };

    internal static ParsedModuleArtifact Read(string configuredRoot, WorkflowModuleLoadOptions options)
    {
        options.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredRoot);
        var root = Path.GetFullPath(configuredRoot);
        if (!Directory.Exists(root)) Fail("EW7001_MODULE_DIRECTORY_NOT_FOUND", $"Module directory '{root}' does not exist.");
        RejectReparsePoint(root, "EW7002_MODULE_PATH_REPARSE_POINT");

        var manifestPath = ResolveFile(root, "module.json", options.MaximumManifestBytes, "manifest");
        ManifestDto dto;
        try
        {
            using var stream = File.OpenRead(manifestPath);
            dto = JsonSerializer.Deserialize<ManifestDto>(stream, JsonOptions)
                ?? throw new JsonException("The manifest is empty.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new WorkflowModuleLoadException("EW7003_INVALID_MODULE_MANIFEST",
                $"Manifest '{manifestPath}' is not valid strict JSON.", exception);
        }

        if (dto.SchemaVersion != WorkflowModuleLoadOptions.CurrentManifestSchemaVersion)
            Fail("EW7004_UNSUPPORTED_MANIFEST_SCHEMA", $"Manifest schema {dto.SchemaVersion} is not supported.");
        if (dto.ContractVersion != WorkflowModuleLoadOptions.CurrentContractVersion)
            Fail("EW7005_INCOMPATIBLE_MODULE_CONTRACT", $"Module contract {dto.ContractVersion} is not supported.");
        if (!TechnicalId.IsValid(dto.Id)) Fail("EW7003_INVALID_MODULE_MANIFEST", "Module id is invalid.");
        if (dto.Version is null || !SemanticVersion().IsMatch(dto.Version))
            Fail("EW7003_INVALID_MODULE_MANIFEST", "Module version must be a canonical semantic version.");
        if (string.IsNullOrWhiteSpace(dto.EntryType) || dto.EntryType.Length > 512)
            Fail("EW7003_INVALID_MODULE_MANIFEST", "Module entry type is missing or too long.");

        var entryRelativePath = RequiredPath(dto.EntryAssembly, "entryAssembly");
        var expectedEntryHash = RequiredSha256(dto.EntryAssemblySha256, "entryAssemblySha256");
        var entryPath = ResolveFile(root, entryRelativePath, options.MaximumAssemblyBytes, "entry assembly");
        VerifyHash(entryPath, expectedEntryHash, "EW7006_MODULE_FILE_HASH_MISMATCH");

        var dependencies = dto.Dependencies ?? [];
        if (dependencies.Count > options.MaximumDependencies)
            Fail("EW7003_INVALID_MODULE_MANIFEST", "The dependency count exceeds the configured limit.");
        var parsedDependencies = dependencies.Select(item =>
        {
            var path = RequiredPath(item.Path, "dependency path");
            var hash = RequiredSha256(item.Sha256, $"dependency '{path}' hash");
            var fullPath = ResolveFile(root, path, options.MaximumAssemblyBytes, "dependency");
            VerifyHash(fullPath, hash, "EW7006_MODULE_FILE_HASH_MISMATCH");
            return new ParsedDependency(new WorkflowModuleDependency(path, hash), fullPath);
        }).ToImmutableArray();
        EnsureDistinct(parsedDependencies.Select(item => item.Model.Path), "dependency paths");

        var resources = dto.Resources ?? [];
        if (resources.Count > options.MaximumResources)
            Fail("EW7003_INVALID_MODULE_MANIFEST", "The resource count exceeds the configured limit.");
        var parsedResources = resources.Select(item =>
        {
            var path = RequiredPath(item.Path, "resource path");
            if (item.ContentType is null || !AllowedResourceContentTypes.Contains(item.ContentType))
                Fail("EW7007_MODULE_RESOURCE_TYPE_FORBIDDEN", $"Resource '{path}' has a forbidden content type.");
            var hash = RequiredSha256(item.Sha256, $"resource '{path}' hash");
            var fullPath = ResolveFile(root, path, options.MaximumResourceBytes, "resource");
            VerifyHash(fullPath, hash, "EW7006_MODULE_FILE_HASH_MISMATCH");
            var model = new WorkflowModuleResource(path, item.ContentType, hash, new FileInfo(fullPath).Length);
            return new ParsedResource(model, fullPath);
        }).ToImmutableArray();
        EnsureDistinct(parsedResources.Select(item => item.Model.Path), "resource paths");

        var components = dto.Components ?? [];
        if (components.Count > options.MaximumComponents)
            Fail("EW7003_INVALID_MODULE_MANIFEST", "The component count exceeds the configured limit.");
        var parsedComponents = components.Select(item =>
        {
            if (!TechnicalId.IsValid(item.Id)) Fail("EW7003_INVALID_MODULE_MANIFEST", "A component id is invalid.");
            if (string.IsNullOrWhiteSpace(item.Type) || item.Type.Length > 512)
                Fail("EW7003_INVALID_MODULE_MANIFEST", $"Component '{item.Id}' has an invalid type name.");
            return (new TechnicalId(item.Id!), item.Type);
        }).ToImmutableArray();
        EnsureDistinct(parsedComponents.Select(item => item.Item1.Value), "component ids");

        var manifest = new WorkflowModuleManifest(dto.SchemaVersion, new TechnicalId(dto.Id!), dto.Version,
            dto.ContractVersion, entryRelativePath, dto.EntryType!, expectedEntryHash,
            parsedDependencies.Select(item => item.Model).ToImmutableArray(),
            parsedResources.Select(item => item.Model).ToImmutableArray(), parsedComponents);
        return new ParsedModuleArtifact(root, manifest, entryPath, parsedDependencies, parsedResources,
            ComputeArtifactHash(manifest));
    }

    private static string ComputeArtifactHash(WorkflowModuleManifest manifest)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, manifest.Id.Value);
        Append(hash, manifest.Version);
        Append(hash, manifest.ContractVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, manifest.EntryAssembly);
        Append(hash, manifest.EntryType);
        Append(hash, manifest.EntryAssemblySha256);
        foreach (var item in manifest.Dependencies.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            Append(hash, item.Path);
            Append(hash, item.Sha256);
        }
        foreach (var item in manifest.Resources.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            Append(hash, item.Path);
            Append(hash, item.ContentType);
            Append(hash, item.Sha256);
        }
        foreach (var item in manifest.Components.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            Append(hash, item.Id.Value);
            Append(hash, item.TypeName);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static string ResolveFile(string root, string relativePath, long maximumLength, string kind)
    {
        var path = RequiredPath(relativePath, kind);
        var segments = path.Split('/');
        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current)) RejectReparsePoint(current, "EW7002_MODULE_PATH_REPARSE_POINT");
        }
        var fullPath = Path.GetFullPath(current);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, comparison) || !File.Exists(fullPath))
            Fail("EW7008_MODULE_FILE_NOT_FOUND", $"Declared {kind} '{path}' does not exist below the module directory.");
        var length = new FileInfo(fullPath).Length;
        if (length > maximumLength) Fail("EW7009_MODULE_FILE_TOO_LARGE", $"Declared {kind} '{path}' exceeds its size limit.");
        return fullPath;
    }

    private static string RequiredPath(string? path, string field)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 512 || path.Contains('\\') ||
            Path.IsPathRooted(path) || path.Split('/').Any(segment => segment is "" or "." or ".."))
            Fail("EW7003_INVALID_MODULE_MANIFEST", $"The {field} must be a normalized relative path using '/'.");
        return path;
    }

    private static string RequiredSha256(string? hash, string field)
    {
        if (hash is null || hash.Length != 64 || hash.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            Fail("EW7003_INVALID_MODULE_MANIFEST", $"The {field} must be a lowercase SHA-256 value.");
        return hash;
    }

    private static void VerifyHash(string path, string expected, string code)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            Fail(code, $"Declared hash does not match '{Path.GetFileName(path)}'.");
    }

    private static void RejectReparsePoint(string path, string code)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            Fail(code, $"Module path '{path}' cannot contain a symbolic link or reparse point.");
    }

    private static void EnsureDistinct(IEnumerable<string> values, string field)
    {
        var items = values.ToArray();
        if (items.Distinct(StringComparer.Ordinal).Count() != items.Length)
            Fail("EW7003_INVALID_MODULE_MANIFEST", $"Manifest {field} must be unique using ordinal comparison.");
    }

    [DoesNotReturn]
    private static void Fail(string code, string message) => throw new WorkflowModuleLoadException(code, message);

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersion();

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ManifestDto
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("contractVersion")] public int ContractVersion { get; set; }
        [JsonPropertyName("entryAssembly")] public string? EntryAssembly { get; set; }
        [JsonPropertyName("entryType")] public string? EntryType { get; set; }
        [JsonPropertyName("entryAssemblySha256")] public string? EntryAssemblySha256 { get; set; }
        [JsonPropertyName("dependencies")] public List<DependencyDto>? Dependencies { get; set; }
        [JsonPropertyName("resources")] public List<ResourceDto>? Resources { get; set; }
        [JsonPropertyName("components")] public List<ComponentDto>? Components { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class DependencyDto
    {
        [JsonPropertyName("path")] public string? Path { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ResourceDto
    {
        [JsonPropertyName("path")] public string? Path { get; set; }
        [JsonPropertyName("contentType")] public string? ContentType { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ComponentDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}

internal sealed record ParsedDependency(WorkflowModuleDependency Model, string FullPath);
internal sealed record ParsedResource(WorkflowModuleResource Model, string FullPath);
internal sealed record ParsedModuleArtifact(
    string RootPath,
    WorkflowModuleManifest Manifest,
    string EntryAssemblyPath,
    ImmutableArray<ParsedDependency> Dependencies,
    ImmutableArray<ParsedResource> Resources,
    string ArtifactSha256);

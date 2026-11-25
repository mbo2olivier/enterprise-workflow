namespace EnterpriseWorkflow.Extensions;

public sealed record WorkflowModuleDiscoveryDiagnostic(string Code, string Path, string Message);

/// <summary>Startup scan bounds. Exceeding a bound fails rather than returning a partial catalog.</summary>
public sealed class WorkflowModuleDiscoveryOptions
{
    public int MaximumDepth { get; set; } = 8;
    public int MaximumDirectories { get; set; } = 10000;
    public int MaximumCandidates { get; set; } = 1024;
    public int MaximumEntries { get; set; } = 100000;
    public Action<WorkflowModuleDiscoveryDiagnostic>? ReportDiagnostic { get; set; }
    internal void Validate()
    {
        if (MaximumDepth is < 0 or > 64) Invalid(nameof(MaximumDepth));
        if (MaximumDirectories is < 1 or > 1000000) Invalid(nameof(MaximumDirectories));
        if (MaximumCandidates is < 1 or > 100000) Invalid(nameof(MaximumCandidates));
        if (MaximumEntries is < 1 or > 10000000) Invalid(nameof(MaximumEntries));
    }
    private static void Invalid(string name) => throw new ArgumentOutOfRangeException(name, "Discovery limit is outside its supported bounds.");
}

public static class WorkflowModuleDiscovery
{
    public static IReadOnlyList<string> FindArtifacts(string rootDirectory, WorkflowModuleDiscoveryOptions? options = null)
    {
        options ??= new(); options.Validate();
        string root;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
            root = Path.GetFullPath(rootDirectory);
            var attributes = File.GetAttributes(root);
            if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("Discovery root must be a directory, not a symbolic link.", nameof(rootDirectory));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new WorkflowModuleLoadException("EW7060_INVALID_DISCOVERY_ROOT", "The configured module discovery root is invalid or inaccessible.", exception);
        }
        var candidates = new List<string>();
        var pending = new Stack<(string Path, int Depth)>(); pending.Push((root, 0));
        var directories = 0; var entries = 0;
        while (pending.TryPop(out var current))
        {
            try
            {
                if (++directories > options.MaximumDirectories) Limit("MaximumDirectories", current.Path);
                var currentAttributes = File.GetAttributes(current.Path);
                if (currentAttributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new WorkflowModuleLoadException("EW7061_DISCOVERY_SCAN_FAILED", $"Directory changed into a symbolic link during discovery: {current.Path}");
                var manifestPath = Path.Combine(current.Path, "module.json");
                FileAttributes? manifestAttributes = null;
                try { manifestAttributes = File.GetAttributes(manifestPath); }
                catch (FileNotFoundException) { }
                if (manifestAttributes is { } manifestFileAttributes)
                {
                    if (manifestFileAttributes.HasFlag(FileAttributes.ReparsePoint) || manifestFileAttributes.HasFlag(FileAttributes.Directory))
                        throw new WorkflowModuleLoadException("EW7003_INVALID_MODULE_MANIFEST", $"Manifest must be a regular file: {manifestPath}");
                    if (candidates.Count >= options.MaximumCandidates) Limit("MaximumCandidates", current.Path);
                    candidates.Add(current.Path);
                    continue;
                }
                var children = new List<string>();
                foreach (var entry in new DirectoryInfo(current.Path).EnumerateFileSystemInfos())
                {
                    if (++entries > options.MaximumEntries) Limit("MaximumEntries", current.Path);
                    var attributes = entry.Attributes;
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        options.ReportDiagnostic?.Invoke(new("EW7063_DISCOVERY_LINK_SKIPPED", entry.FullName, "Symbolic link or junction was not followed."));
                        continue;
                    }
                    if (!attributes.HasFlag(FileAttributes.Directory)) continue;
                    if (current.Depth >= options.MaximumDepth) Limit("MaximumDepth", entry.FullName);
                    if (directories + pending.Count + children.Count >= options.MaximumDirectories) Limit("MaximumDirectories", entry.FullName);
                    children.Add(entry.FullName);
                }
                foreach (var child in children.OrderDescending(StringComparer.Ordinal)) pending.Push((child, current.Depth + 1));
            }
            catch (WorkflowModuleLoadException) { throw; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                throw new WorkflowModuleLoadException("EW7061_DISCOVERY_SCAN_FAILED", $"Module discovery was incomplete at '{current.Path}'.", exception);
            }
        }
        return candidates.AsReadOnly();
    }

    private static void Limit(string limit, string path) =>
        throw new WorkflowModuleLoadException("EW7062_DISCOVERY_LIMIT_EXCEEDED", $"Module discovery exceeded {limit} at '{path}'. No partial catalog was accepted.");
}

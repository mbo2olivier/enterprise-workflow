using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Presentation;

internal sealed class DevelopmentConfiguration
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public int SchemaVersion { get; set; } = 1;
    public DevelopmentServer Server { get; set; } = new();
    public DevelopmentStorage Storage { get; set; } = new();
    public string[] ModuleDirectories { get; set; } = [];
    public bool Watch { get; set; } = true;
    public DevelopmentSeedOptions Seed { get; set; } = new();
    public WorkflowSmtpOptions Smtp { get; set; } = new();

    internal static async Task<DevelopmentConfiguration> ReadAsync(string project, string? file, string? url, bool noWatch)
    {
        var root = Path.GetDirectoryName(project)!;
        var path = file is null ? Path.Combine(root, "ew.dev.json") : Path.GetFullPath(file);
        if (file is not null && !File.Exists(path)) throw new ArgumentException($"Configuration not found: {path}");
        var config = File.Exists(path)
            ? JsonSerializer.Deserialize<DevelopmentConfiguration>(await File.ReadAllTextAsync(path), Options)
                ?? throw new ArgumentException("Empty development configuration.")
            : new DevelopmentConfiguration();
        if (config.Server is null || config.Storage is null || config.Seed is null || config.Smtp is null ||
            config.ModuleDirectories is null || config.Seed.Users is null || config.Seed.Users.Length > 100 || config.Seed.Users.Any(user => user is null || user.Permissions is null))
            throw new ArgumentException("Development configuration sections must be non-null; at most 100 seed users are supported.");
        if (config.SchemaVersion != 1) throw new ArgumentException("Unsupported configuration schemaVersion.");
        if (url is not null) config.Server.Url = url;
        if (noWatch) config.Watch = false;
        if (!Uri.TryCreate(config.Server.Url, UriKind.Absolute, out var address) || address.Scheme != "http" ||
            !(address.Host == "localhost" || IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip)) ||
            address.Port < 1 || address.AbsolutePath != "/" || address.Query.Length != 0 || address.UserInfo.Length != 0 || address.Fragment.Length != 0)
            throw new ArgumentException("Development URL must be an HTTP loopback address, e.g. http://localhost:5080.");
        if (config.Storage.Provider != "sqlite") throw new ArgumentException("Development storage supports sqlite only.");
        config.Storage.Path = Path.GetFullPath(config.Storage.Path, File.Exists(path) ? Path.GetDirectoryName(path)! : root);
        config.ModuleDirectories = config.ModuleDirectories.Select(directory => Path.GetFullPath(directory,
            File.Exists(path) ? Path.GetDirectoryName(path)! : root)).ToArray();
        config.Smtp.Validate();
        if (config.Seed.Users.Select(user => user.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != config.Seed.Users.Length ||
            config.Seed.Users.Any(user => string.IsNullOrWhiteSpace(user.Id) || user.Id.Length > 64 || user.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')))
            throw new ArgumentException("Seed IDs must be unique, at most 64 ASCII letters, digits or hyphens.");
        return config;
    }

    internal Dictionary<string, string?> ToHostConfiguration()
    {
        var values = new Dictionary<string, string?> { ["Installation:Id"] = "ew-development" };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Smtp));
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
                foreach (var item in property.Value.EnumerateObject()) values[$"Notifications:Smtp:{property.Name}:{item.Name}"] = item.Value.ToString();
            else if (property.Value.ValueKind is not JsonValueKind.Null) values[$"Notifications:Smtp:{property.Name}"] = property.Value.ToString();
        }
        return values;
    }
}
internal sealed class DevelopmentServer { public string Url { get; set; } = "http://localhost:5080"; }
internal sealed class DevelopmentStorage
{
    public string Provider { get; set; } = "sqlite";
    public string Path { get; set; } = ".ew/dev/workflow.db";
}
internal sealed class DevelopmentSeedOptions { public DevelopmentUser[] Users { get; set; } = [new() { Id = "alice", DisplayName = "Alice" }, new() { Id = "bob", DisplayName = "Bob" }, new() { Id = "carol", DisplayName = "Carol" }]; }
internal sealed class DevelopmentUser
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string[] Permissions { get; set; } = [];
}

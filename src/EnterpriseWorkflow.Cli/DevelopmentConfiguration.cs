using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Presentation;

internal sealed class DevelopmentConfiguration
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public int SchemaVersion { get; set; } = 2;
    public string DefaultEnvironment { get; set; } = "default";
    public DevelopmentBuild Build { get; set; } = new();
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> Environments { get; set; } = new(StringComparer.Ordinal);
    public EnterpriseWorkflow.Core.Serialization.WorkflowSerializationOptions Serialization { get; set; } = new();
    [JsonIgnore] public string EnvironmentName { get; private set; } = "default";
    [JsonIgnore] public string StateRoot { get; private set; } = string.Empty;
    public DevelopmentServer Server { get; set; } = new();
    public DevelopmentStorage Storage { get; set; } = new();
    public string[] ModuleDirectories { get; set; } = [];
    public bool Watch { get; set; } = true;
    public DevelopmentSeedOptions Seed { get; set; } = new();
    public WorkflowSmtpOptions Smtp { get; set; } = new();

    internal static async Task<DevelopmentConfiguration> ReadAsync(string project, string? file, string? url, bool noWatch,
        string? environment = null, string? configuration = null)
    {
        var root = Path.GetDirectoryName(project)!;
        var path = file is null ? Path.Combine(root, "ew.dev.json") : Path.GetFullPath(file);
        if (file is not null && !File.Exists(path)) throw new ArgumentException($"Configuration not found: {path}");
        var node = File.Exists(path) ? System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path)) as System.Text.Json.Nodes.JsonObject
            ?? throw new JsonException("Development configuration must be an object.") : new System.Text.Json.Nodes.JsonObject();
        var selected = environment ?? node["defaultEnvironment"]?.GetValue<string>() ?? "default";
        ValidateEnvironment(selected); selected = selected.ToLowerInvariant();
        if (node["environments"] is { } overrides)
        {
            if (overrides is not System.Text.Json.Nodes.JsonObject environments) throw new JsonException("environments must be an object.");
            if (environments.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != environments.Count) throw new JsonException("Environment names must be unique without case differences.");
            foreach (var entry in environments)
            {
                ValidateEnvironment(entry.Key);
                if (entry.Value is not System.Text.Json.Nodes.JsonObject value) throw new JsonException("An environment override must be an object.");
                if (value.Any(pair => pair.Key is not ("build" or "server" or "storage" or "seed" or "smtp" or "serialization" or "moduleDirectories" or "watch")))
                    throw new JsonException("Unsupported environment override.");
            }
            if (environments.FirstOrDefault(pair => pair.Key.Equals(selected, StringComparison.OrdinalIgnoreCase)).Value is System.Text.Json.Nodes.JsonObject chosen)
            {
                node.Remove("environments"); Merge(node, chosen);
            }
        }
        var config = node.Deserialize<DevelopmentConfiguration>(Options) ?? throw new JsonException("Empty configuration.");
        if (config.SchemaVersion is not (1 or 2)) throw new ArgumentException("Unsupported configuration schemaVersion.");
        if (config.Server is null || config.Storage is null || config.Build is null || config.Serialization is null || config.Seed is null || config.Smtp is null || config.ModuleDirectories is null ||
            config.Seed.Users is null || config.Seed.Users.Length > 100 || config.Seed.Users.Any(user => user is null || user.Permissions is null))
            throw new ArgumentException("Development sections must be non-null; at most 100 seed users are supported.");
        config.EnvironmentName = selected;
        config.StateRoot = Path.Combine(root, ".ew", "environments", selected);
        if (url is not null) config.Server.Url = url;
        config.Watch = node["build"]?["watch"] is not null ? config.Build.Watch : node["watch"] is not null ? config.Watch : config.Build.Watch;
        if (noWatch) config.Watch = false;
        if (configuration is not null) config.Build.Configuration = configuration;
        if (config.Build.Configuration is not ("Debug" or "Release")) throw new ArgumentException("Configuration must be Debug or Release.");
        if (!Uri.TryCreate(config.Server.Url, UriKind.Absolute, out var address) || address.Scheme != "http" ||
            !(address.Host == "localhost" || IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip)) ||
            address.Port < 1 || address.AbsolutePath != "/" || address.Query.Length != 0 || address.UserInfo.Length != 0 || address.Fragment.Length != 0)
            throw new ArgumentException("Development URL must be an HTTP loopback address, e.g. http://localhost:5080.");
        if (config.Storage.Provider != "sqlite") throw new ArgumentException("Development storage supports sqlite only.");
        var configurationRoot = File.Exists(path) ? Path.GetDirectoryName(path)! : root;
        config.Storage.Path = config.Storage.Path is { } databasePath ? Path.GetFullPath(databasePath, configurationRoot) : Path.Combine(config.StateRoot, "workflow.db");
        config.ModuleDirectories = config.ModuleDirectories.Select(directory => Path.GetFullPath(directory, configurationRoot)).ToArray();
        config.Smtp.Validate(); config.Serialization.Validate();
        if (config.Seed.Users.Select(user => user.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != config.Seed.Users.Length ||
            config.Seed.Users.Any(user => string.IsNullOrWhiteSpace(user.Id) || user.Id.Length > 64 || user.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')))
            throw new ArgumentException("Seed IDs must be unique and contain at most 64 ASCII letters, digits or hyphens.");
        return config;
    }
    private static void Merge(System.Text.Json.Nodes.JsonObject target, System.Text.Json.Nodes.JsonObject overlay)
    {
        foreach (var pair in overlay)
            if (pair.Value is System.Text.Json.Nodes.JsonObject child && target[pair.Key] is System.Text.Json.Nodes.JsonObject parent) Merge(parent, child);
            else target[pair.Key] = pair.Value?.DeepClone();
    }
    internal static void ValidateEnvironment(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || !char.IsAsciiLetterOrDigit(name[0]) || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')) ||
            name.Equals("CON", StringComparison.OrdinalIgnoreCase) || name.Equals("PRN", StringComparison.OrdinalIgnoreCase) || name.Equals("AUX", StringComparison.OrdinalIgnoreCase) || name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            name.Length == 4 && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && char.IsAsciiDigit(name[3]))
            throw new ArgumentException("Invalid development environment name.");
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
    public string? Path { get; set; }
}
internal sealed class DevelopmentSeedOptions { public DevelopmentUser[] Users { get; set; } = [new() { Id = "alice", DisplayName = "Alice" }, new() { Id = "bob", DisplayName = "Bob" }, new() { Id = "carol", DisplayName = "Carol" }]; }
internal sealed class DevelopmentUser
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string[] Permissions { get; set; } = [];
}

internal sealed class DevelopmentBuild { public string Configuration { get; set; } = "Debug"; public bool Watch { get; set; } = true; }

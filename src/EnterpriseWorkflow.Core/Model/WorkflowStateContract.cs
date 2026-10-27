using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Core.Model;

/// <summary>Portable state schema stored with the immutable workflow definition.</summary>
public sealed record WorkflowStateContract(int Version, WorkflowStateField Root, WorkflowStateValidatorBinding[]? Validators = null)
{
    public static WorkflowStateContract? From(WorkflowDefinition definition) =>
        WorkflowStagePolicy.Read(definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start).Configuration)?.StateContract;

    public void EnsureValid()
    {
        if (Version <= 0 || Root is null || Root.Kind != "object") throw new FormatException("Invalid state contract root or version.");
        Check(Root, 0);
        if (Validators is { } validators && (validators.Length > 32 || validators.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 1024 ||
            item.Groups is null || item.Groups.Length > 32 || item.Groups.Any(group => !TechnicalId.IsValid(group)))))
            throw new FormatException("Invalid custom state validator bindings.");
        static void Check(WorkflowStateField field, int depth)
        {
            if (depth > 12 || field is null || field.Fields is null || field.Fields.Length > 128 || field.RequiredGroups is null || field.PermissionGroups is null ||
                field.Name is null || field.Name.Length > 128 || field.Label is null || field.Label.Length > 200 ||
                field.MinimumLength is < 0 || field.MaximumLength is < 0 || field.MinimumLength > field.MaximumLength || field.Minimum > field.Maximum ||
                field.RequiredGroups.Length > 32 || field.RequiredGroups.Any(group => !TechnicalId.IsValid(group)) || field.PermissionGroups.Length > 128 ||
                field.PermissionGroups.Any(group => group is null || group.Length is 0 or > 128 || group.Any(permission => !TechnicalId.IsValid(permission))))
                throw new FormatException("Invalid state field metadata.");
            if (field.PresentationRole is not (null or "media" or "comments")) throw new FormatException("Unsupported state presentation role.");
            if (field.Kind is not ("object" or "array" or "string" or "date" or "datetime" or "guid" or "integer" or "number" or "boolean"))
                throw new FormatException("Unsupported state field kind.");
            if (field.Fields.Any(child => child is null || string.IsNullOrWhiteSpace(child.Name)) || field.Fields.Select(child => child.Name).Distinct(StringComparer.Ordinal).Count() != field.Fields.Length)
                throw new FormatException("Invalid or duplicate state property name.");
            foreach (var child in field.Fields) Check(child, depth + 1);
            if (field.Kind == "array" && field.Item is null) throw new FormatException("An array state field requires an item contract.");
            if (field.Item is not null) Check(field.Item, depth + 1);
        }
    }

    public IReadOnlyList<string> Validate(CanonicalJson state, IReadOnlyCollection<string>? groups = null)
    {
        var errors = new List<string>();
        ValidateField(Root, state.ToJsonElement(), "$", groups ?? [], errors);
        return errors;
    }

    private static void ValidateField(WorkflowStateField field, JsonElement value, string path,
        IReadOnlyCollection<string> groups, List<string> errors)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            if (!field.Nullable || field.Required || field.RequiredGroups.Any(groups.Contains)) errors.Add(path);
            return;
        }
        var valid = field.Kind switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" or "date" or "datetime" or "guid" => value.ValueKind == JsonValueKind.String,
            "number" => value.ValueKind == JsonValueKind.Number,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false,
        };
        if (!valid) { errors.Add(path); return; }
        if (field.Kind == "object")
        {
            foreach (var property in value.EnumerateObject())
                if (!field.Fields.Any(child => child.Name == property.Name)) errors.Add(path + "." + property.Name);
            foreach (var child in field.Fields)
            {
                if (value.TryGetProperty(child.Name, out var childValue)) ValidateField(child, childValue, path + "." + child.Name, groups, errors);
                else if (!child.Nullable || child.Required || child.RequiredGroups.Any(groups.Contains)) errors.Add(path + "." + child.Name);
            }
        }
        else if (field.Kind == "array")
        {
            foreach (var item in value.EnumerateArray()) ValidateField(field.Item!, item, path + "[]", groups, errors);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (((field.Required || field.RequiredGroups.Any(groups.Contains)) && string.IsNullOrWhiteSpace(text)) ||
                (field.MinimumLength is { } minimum && text.Length < minimum) ||
                (field.MaximumLength is { } maximum && text.Length > maximum) ||
                (field.Kind == "date" && !DateOnly.TryParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)) ||
                (field.Kind == "datetime" && !DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)) ||
                (field.Kind == "guid" && !Guid.TryParse(text, out _))) errors.Add(path);
        }
        else if (value.ValueKind == JsonValueKind.Number &&
            (!value.TryGetDecimal(out var number) || (field.Minimum is { } min && number < min) || (field.Maximum is { } max && number > max))) errors.Add(path);
    }

    public CanonicalJson Project(CanonicalJson state, IReadOnlySet<string> permissions)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(Root, state.ToJsonElement(), writer, permissions);
        return CanonicalJson.CreateObject(System.Text.Encoding.UTF8.GetString(stream.ToArray()), WorkflowDefinitionLimits.Default.MaximumStateBytes);
    }

    private static void Write(WorkflowStateField field, JsonElement value, Utf8JsonWriter writer, IReadOnlySet<string> permissions)
    {
        if ((value.ValueKind == JsonValueKind.Object && field.Kind != "object") ||
            (value.ValueKind == JsonValueKind.Array && field.Kind != "array") ||
            (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null) && field.Kind is "object" or "array"))
        {
            writer.WriteNullValue();
            return;
        }
        if (value.ValueKind == JsonValueKind.Object && field.Kind == "object")
        {
            writer.WriteStartObject();
            foreach (var child in field.Fields)
            {
                if (child.Hidden || !child.PermissionGroups.All(group => group.Any(permissions.Contains)) || !value.TryGetProperty(child.Name, out var childValue)) continue;
                writer.WritePropertyName(child.Name);
                Write(child, childValue, writer, permissions);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array && field.Item is not null)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) Write(field.Item, item, writer, permissions);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}

public sealed record WorkflowStateField
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PresentationRole { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Kind { get; init; } = "object";
    public bool Nullable { get; init; }
    public bool Required { get; init; }
    public bool Hidden { get; init; }
    public string[] RequiredGroups { get; init; } = [];
    public string[][] PermissionGroups { get; init; } = [];
    public int? MinimumLength { get; init; }
    public int? MaximumLength { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public WorkflowStateField[] Fields { get; init; } = [];
    public WorkflowStateField? Item { get; init; }
}

public sealed record WorkflowStateValidatorBinding(string Id, string[] Groups);

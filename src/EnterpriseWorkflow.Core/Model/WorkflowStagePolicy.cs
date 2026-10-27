using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Core.Model;

/// <summary>Versioned authoring contract stored in canonical node configuration. Absent on historical definitions.</summary>
public sealed record WorkflowStagePolicy
{
    public const string PropertyName = "$workflow";
    public int Version { get; init; } = 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowCompletionOutcome? CompletionOutcome { get; init; }
    public string[][] Permissions { get; init; } = [];
    public string[][] ReadPermissions { get; init; } = [];
    public string[][] CancelPermissions { get; init; } = [];
    public bool AssignedToInitiator { get; init; }
    public bool AssignedFromPreviousTask { get; init; }
    public string[] SummaryFields { get; init; } = [];
    public Dictionary<string, WorkflowActionPolicy> Actions { get; init; } = new(StringComparer.Ordinal);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowStateContract? StateContract { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? StateValidationGroups { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, WorkflowMediaPolicy>? MediaPolicies { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaPolicyModule { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaPolicyModuleVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? MediaFields { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static WorkflowStagePolicy? Read(CanonicalJson configuration)
    {
        var root = configuration.ToJsonElement();
        if (!root.TryGetProperty(PropertyName, out var element)) return null;
        var policy = element.Deserialize<WorkflowStagePolicy>(JsonOptions)
            ?? throw new FormatException("A workflow policy must be an object.");
        policy.Validate();
        return policy;
    }

    public string ToConfigurationJson() => JsonSerializer.Serialize(new Dictionary<string, WorkflowStagePolicy>
    {
        [PropertyName] = this,
    }, JsonOptions);

    public void Validate()
    {
        if (CompletionOutcome is { } completion && !Enum.IsDefined(completion)) throw new FormatException("Invalid completion outcome.");
        if (Version != 1) throw new FormatException("Unsupported workflow policy version.");
        if (StateValidationGroups is { } validationGroups && (validationGroups.Length > 32 || validationGroups.Any(group => !TechnicalId.IsValid(group))))
            throw new FormatException("Invalid state validation groups.");
        StateContract?.EnsureValid();
        if (MediaPolicyModule is not null && !TechnicalId.IsValid(MediaPolicyModule)) throw new FormatException("Invalid media policy module.");
        if (MediaPolicies is { } mediaPolicies) { if (mediaPolicies.Count > 64) throw new FormatException("Too many media policies."); foreach (var (id, mediaPolicy) in mediaPolicies) { mediaPolicy.Validate(); if (id != mediaPolicy.Id) throw new FormatException("Invalid media policy key."); } }
        if (MediaFields is { } mediaFields && (mediaFields.Count > 128 || mediaFields.Any(field => !TechnicalId.IsValid(field.Key) || !TechnicalId.IsValid(field.Value)))) throw new FormatException("Invalid media fields.");
        ValidateGroups(Permissions);
        ValidateGroups(ReadPermissions);
        ValidateGroups(CancelPermissions);
        if (SummaryFields is null || SummaryFields.Length > 128 || SummaryFields.Any(id => !TechnicalId.IsValid(id)) ||
            SummaryFields.Distinct(StringComparer.Ordinal).Count() != SummaryFields.Length)
            throw new FormatException("Summary fields must have unique valid identifiers.");
        if (Actions is null || Actions.Count > 128) throw new FormatException("Invalid workflow policy actions.");
        foreach (var (id, action) in Actions)
        {
            if (!TechnicalId.IsValid(id) || action is null) throw new FormatException("Invalid workflow policy action.");
            if (action.StateValidationGroups is { } actionGroups && (actionGroups.Length > 32 || actionGroups.Any(group => !TechnicalId.IsValid(group))))
                throw new FormatException("Invalid action state validation groups.");
            if (action.CommentPath is { } commentPath && (commentPath.Length is 0 or > 12 || commentPath.Any(part => string.IsNullOrWhiteSpace(part) || part.Length > 128)))
                throw new FormatException("Invalid comment state path.");
            ValidateGroups(action.Permissions);
            if (string.IsNullOrWhiteSpace(action.Label) || action.Label.Length > 200 ||
                (action.AssigneeTarget is not null && !TechnicalId.IsValid(action.AssigneeTarget)))
                throw new FormatException("Invalid workflow action label or assignment target.");
        }
        if (AssignedToInitiator && AssignedFromPreviousTask)
            throw new FormatException("A task cannot combine initiator and previous-task assignment.");
    }

    private static void ValidateGroups(string[][] groups)
    {
        if (groups is null || groups.Length > 128 || groups.Any(group => group is null || group.Length is 0 or > 128 ||
                group.Any(id => !TechnicalId.IsValid(id)) || group.Distinct(StringComparer.Ordinal).Count() != group.Length))
            throw new FormatException("Permission requirements must contain bounded nonempty groups of valid identifiers.");
    }
}

/// <summary>One human action's presentation, additional permission groups and optional direct assignment.</summary>
public sealed record WorkflowActionPolicy
{
    public string Label { get; init; } = string.Empty;
    public string[][] Permissions { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CaptureCommentActorName { get; init; }
    public bool RequireComment { get; init; }
    public string? AssigneeTarget { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? StateValidationGroups { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? CommentPath { get; init; }
}

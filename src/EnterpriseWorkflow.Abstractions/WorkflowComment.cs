using System.Text.Json.Serialization;

namespace EnterpriseWorkflow.Abstractions;

/// <summary>Append-only approval comment with identity and execution metadata supplied by the server.</summary>
public sealed record WorkflowComment(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("stageId")] string StageId,
    [property: JsonPropertyName("actionId")] string ActionId,
    [property: JsonPropertyName("actorProviderId")] string ActorProviderId,
    [property: JsonPropertyName("actorSubjectId")] string ActorSubjectId,
    [property: JsonPropertyName("createdAtUtc")] DateTimeOffset CreatedAtUtc)
{
    [JsonPropertyName("actorDisplayName")] public string? ActorDisplayName { get; init; }
}

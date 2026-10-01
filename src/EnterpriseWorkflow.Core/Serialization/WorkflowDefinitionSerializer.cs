using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Core.Serialization;

internal static class WorkflowDefinitionSerializer
{
    public static string SerializeCanonical(
        string definitionId,
        int definitionVersion,
        string? displayLabel,
        ImmutableArray<WorkflowArtifactReference> artifacts,
        ImmutableArray<WorkflowNode> nodes,
        ImmutableArray<WorkflowTransition> transitions)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteDefinition(writer, definitionId, definitionVersion, displayLabel, artifacts, nodes, transitions, canonical: true);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static int GetReceivedByteCount(
        string definitionId,
        int definitionVersion,
        string? displayLabel,
        ImmutableArray<WorkflowArtifactReference> artifacts,
        ImmutableArray<WorkflowNode> nodes,
        ImmutableArray<WorkflowTransition> transitions)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteDefinition(writer, definitionId, definitionVersion, displayLabel, artifacts, nodes, transitions, canonical: false);
        }

        return checked((int)stream.Length);
    }

    private static void WriteDefinition(
        Utf8JsonWriter writer,
        string definitionId,
        int definitionVersion,
        string? displayLabel,
        ImmutableArray<WorkflowArtifactReference> artifacts,
        ImmutableArray<WorkflowNode> nodes,
        ImmutableArray<WorkflowTransition> transitions,
        bool canonical)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("artifacts");
        writer.WriteStartArray();
        IEnumerable<WorkflowArtifactReference> orderedArtifacts = canonical
            ? artifacts
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                .ThenBy(item => item.Version, StringComparer.Ordinal)
            : artifacts;
        foreach (var artifact in orderedArtifacts)
        {
            writer.WriteStartObject();
            writer.WriteString("id", artifact.Id.Value);
            writer.WriteString("sha256", artifact.Sha256);
            writer.WriteString("version", artifact.Version);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteString("definitionId", definitionId);
        writer.WriteNumber("definitionVersion", definitionVersion);
        if (displayLabel is null)
        {
            writer.WriteNull("displayLabel");
        }
        else
        {
            writer.WriteString("displayLabel", displayLabel);
        }

        writer.WritePropertyName("nodes");
        writer.WriteStartArray();
        IEnumerable<WorkflowNode> orderedNodes = canonical
            ? nodes.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            : nodes;
        foreach (var node in orderedNodes)
        {
            WriteNode(writer, node, canonical);
        }

        writer.WriteEndArray();
        writer.WriteNumber("schemaVersion", WorkflowDefinition.SchemaVersion);
        writer.WritePropertyName("transitions");
        writer.WriteStartArray();
        IEnumerable<WorkflowTransition> orderedTransitions = canonical
            ? transitions
                .OrderBy(item => item.SourceId.Value, StringComparer.Ordinal)
                .ThenBy(item => item.IsDefault)
                .ThenBy(item => item.Outcome?.Value, StringComparer.Ordinal)
                .ThenBy(item => item.TargetId.Value, StringComparer.Ordinal)
            : transitions;
        foreach (var transition in orderedTransitions)
        {
            WriteTransition(writer, transition);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteNode(Utf8JsonWriter writer, WorkflowNode node, bool canonical)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("configuration");
        if (canonical)
        {
            using var configuration = JsonDocument.Parse(node.Configuration.CanonicalText);
            CanonicalJson.WriteCanonical(writer, configuration.RootElement);
        }
        else
        {
            writer.WriteRawValue(node.Configuration.OriginalText, skipInputValidation: false);
        }

        writer.WritePropertyName("declaredOutcomes");
        writer.WriteStartArray();
        IEnumerable<EnterpriseWorkflow.Abstractions.TechnicalId> outcomes = canonical
            ? node.DeclaredOutcomes.OrderBy(item => item.Value, StringComparer.Ordinal)
            : node.DeclaredOutcomes;
        foreach (var outcome in outcomes)
        {
            writer.WriteStringValue(outcome.Value);
        }

        writer.WriteEndArray();
        if (node.DisplayLabel is null)
        {
            writer.WriteNull("displayLabel");
        }
        else
        {
            writer.WriteString("displayLabel", node.DisplayLabel);
        }

        writer.WritePropertyName("handler");
        if (node.Handler is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString("id", node.Handler.Id.Value);
            writer.WriteNumber("version", node.Handler.Version);
            writer.WriteEndObject();
        }

        writer.WriteString("id", node.Id.Value);
        writer.WriteString("role", node.Role.ToString());
        writer.WritePropertyName("type");
        writer.WriteStartObject();
        writer.WriteString("id", node.Type.Id.Value);
        writer.WriteNumber("version", node.Type.Version);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteTransition(Utf8JsonWriter writer, WorkflowTransition transition)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("default", transition.IsDefault);
        if (transition.Outcome is null)
        {
            writer.WriteNull("outcome");
        }
        else
        {
            writer.WriteString("outcome", transition.Outcome.Value.Value);
        }

        writer.WriteString("source", transition.SourceId.Value);
        writer.WriteString("target", transition.TargetId.Value);
        writer.WriteEndObject();
    }
}

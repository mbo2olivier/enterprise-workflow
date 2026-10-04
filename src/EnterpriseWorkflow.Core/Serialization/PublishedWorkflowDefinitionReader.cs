using System.Collections.Immutable;
using System.Text.Json;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Validation;

namespace EnterpriseWorkflow.Core.Serialization;

/// <summary>Rehydrates and revalidates an immutable definition read from durable storage.</summary>
public static class PublishedWorkflowDefinitionReader
{
    /// <summary>Reads canonical JSON and verifies its durable identity and hash.</summary>
    public static WorkflowDefinition Read(
        string canonicalJson,
        string expectedDefinitionId,
        int expectedVersion,
        string expectedSha256)
    {
        ArgumentException.ThrowIfNullOrEmpty(canonicalJson);
        using var document = JsonDocument.Parse(canonicalJson);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != WorkflowDefinition.SchemaVersion)
        {
            throw new InvalidDataException("The published workflow definition schema is not supported.");
        }

        var draft = new WorkflowDraft(
            root.GetProperty("definitionId").GetString() ?? string.Empty,
            root.GetProperty("definitionVersion").GetInt32(),
            ReadNullableString(root.GetProperty("displayLabel")),
            root.GetProperty("artifacts").EnumerateArray().Select(ReadArtifact),
            root.GetProperty("nodes").EnumerateArray().Select(ReadNode),
            root.GetProperty("transitions").EnumerateArray().Select(ReadTransition));
        var compilation = WorkflowDefinitionCompiler.Compile(draft);
        var definition = compilation.Definition ?? throw new InvalidDataException(
            $"The published workflow definition is invalid: {string.Join(", ", compilation.Diagnostics.Select(item => item.Code))}.");

        if (!string.Equals(definition.Id.Value, expectedDefinitionId, StringComparison.Ordinal) ||
            definition.Version != expectedVersion ||
            !string.Equals(definition.Sha256, expectedSha256, StringComparison.Ordinal) ||
            !string.Equals(definition.CanonicalJson, canonicalJson, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The published workflow definition identity, hash, or canonical representation does not match storage metadata.");
        }

        return definition;
    }

    private static WorkflowArtifactReferenceDraft ReadArtifact(JsonElement element) => new(
        element.GetProperty("id").GetString() ?? string.Empty,
        element.GetProperty("version").GetString() ?? string.Empty,
        element.GetProperty("sha256").GetString() ?? string.Empty);

    private static WorkflowNodeDraft ReadNode(JsonElement element)
    {
        var handler = element.GetProperty("handler");
        var type = element.GetProperty("type");
        return new WorkflowNodeDraft(
            element.GetProperty("id").GetString() ?? string.Empty,
            type.GetProperty("id").GetString() ?? string.Empty,
            type.GetProperty("version").GetInt32(),
            Enum.Parse<WorkflowNodeRole>(element.GetProperty("role").GetString() ?? string.Empty, ignoreCase: false),
            handler.ValueKind is JsonValueKind.Null ? null : handler.GetProperty("id").GetString(),
            handler.ValueKind is JsonValueKind.Null ? null : handler.GetProperty("version").GetInt32(),
            element.GetProperty("configuration").GetRawText(),
            element.GetProperty("declaredOutcomes").EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty).ToImmutableArray(),
            ReadNullableString(element.GetProperty("displayLabel")),
            ReadHumanTask(element.GetProperty("humanTask")),
            ReadTimer(element.GetProperty("timer")));
    }

    private static HumanTaskDefinitionDraft? ReadHumanTask(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        var form = element.GetProperty("form");
        var handler = element.GetProperty("completionHandler");
        return new HumanTaskDefinitionDraft(
            Enum.Parse<HumanTaskKind>(element.GetProperty("kind").GetString() ?? string.Empty, ignoreCase: false),
            Enum.Parse<HumanTaskAssignmentMode>(element.GetProperty("assignmentMode").GetString() ?? string.Empty, ignoreCase: false),
            form.GetProperty("id").GetString() ?? string.Empty,
            form.GetProperty("version").GetInt32(),
            handler.GetProperty("id").GetString() ?? string.Empty,
            handler.GetProperty("version").GetInt32(),
            element.GetProperty("actions").EnumerateArray().Select(item => new HumanTaskActionDraft(
                item.GetProperty("actionId").GetString() ?? string.Empty,
                item.GetProperty("outcome").GetString() ?? string.Empty)).ToImmutableArray(),
            element.GetProperty("allowInitiator").GetBoolean(),
            element.GetProperty("distinctFrom").EnumerateArray().Select(item => new HumanTaskActorConstraintDraft(
                item.GetProperty("nodeId").GetString() ?? string.Empty,
                item.GetProperty("actionId").GetString() ?? string.Empty)).ToImmutableArray());
    }

    private static TimerDefinitionDraft? ReadTimer(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null
            ? null
            : new TimerDefinitionDraft(TimeSpan.FromMilliseconds(element.GetProperty("delayMilliseconds").GetInt64()));

    private static WorkflowTransitionDraft ReadTransition(JsonElement element) => new(
        element.GetProperty("source").GetString() ?? string.Empty,
        element.GetProperty("target").GetString() ?? string.Empty,
        ReadNullableString(element.GetProperty("outcome")),
        element.GetProperty("default").GetBoolean());

    private static string? ReadNullableString(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null ? null : element.GetString();
}

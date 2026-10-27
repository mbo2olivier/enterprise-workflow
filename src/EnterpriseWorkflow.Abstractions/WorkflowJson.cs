using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseWorkflow.Abstractions;

/// <summary>One stable JSON convention for typed workflow inputs and complete state replacements.</summary>
public static class WorkflowJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

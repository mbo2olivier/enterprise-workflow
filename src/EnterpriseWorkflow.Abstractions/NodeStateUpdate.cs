using System.Text.Json;

namespace EnterpriseWorkflow.Abstractions;

/// <summary>
/// Describes whether a node preserves the current state or replaces it in full.
/// </summary>
public sealed class NodeStateUpdate
{
    private readonly JsonElement? _replacement;

    private NodeStateUpdate(JsonElement? replacement)
    {
        _replacement = replacement?.Clone();
    }

    /// <summary>
    /// Gets an update that leaves the durable state unchanged.
    /// </summary>
    public static NodeStateUpdate Unchanged { get; } = new(null);

    /// <summary>
    /// Gets a value indicating whether the complete state is replaced.
    /// </summary>
    public bool ReplacesState => _replacement.HasValue;

    /// <summary>
    /// Creates an update that replaces the complete durable state.
    /// </summary>
    public static NodeStateUpdate Replace(JsonElement replacement)
    {
        if (replacement.ValueKind is not JsonValueKind.Object)
        {
            throw new ArgumentException("Workflow state must be a JSON object.", nameof(replacement));
        }

        return new NodeStateUpdate(replacement);
    }

    /// <summary>
    /// Gets a detached copy of the replacement state, when present.
    /// </summary>
    public JsonElement? GetReplacement() => _replacement?.Clone();
}


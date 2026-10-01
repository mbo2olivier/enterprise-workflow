namespace EnterpriseWorkflow.Core.Model;

/// <summary>
/// A versioned, immutable and size-bounded complete workflow instance state.
/// </summary>
public sealed class WorkflowState
{
    private WorkflowState(int schemaVersion, CanonicalJson value)
    {
        SchemaVersion = schemaVersion;
        Value = value;
    }

    /// <summary>Gets the positive application-defined state schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the complete normalized JSON object.</summary>
    public CanonicalJson Value { get; }

    /// <summary>Creates a state using the configured L2 size and depth limits.</summary>
    public static WorkflowState Create(
        string json,
        int schemaVersion,
        WorkflowDefinitionLimits? limits = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        limits ??= WorkflowDefinitionLimits.Default;
        limits.EnsureValid();
        var value = CanonicalJson.CreateObject(json, limits.MaximumStateBytes, limits.MaximumJsonDepth);
        return new WorkflowState(schemaVersion, value);
    }
}


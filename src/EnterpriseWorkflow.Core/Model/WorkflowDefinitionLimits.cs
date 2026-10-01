namespace EnterpriseWorkflow.Core.Model;

/// <summary>
/// Configurable safety limits applied to workflow definitions and durable JSON values.
/// </summary>
public sealed record WorkflowDefinitionLimits
{
    /// <summary>One mebibyte in bytes.</summary>
    public const int OneMebibyte = 1_048_576;

    /// <summary>Gets the default L2 limits.</summary>
    public static WorkflowDefinitionLimits Default { get; } = new();

    /// <summary>Maximum serialized definition size in UTF-8 bytes.</summary>
    public int MaximumDefinitionBytes { get; init; } = OneMebibyte;

    /// <summary>Maximum number of nodes.</summary>
    public int MaximumNodeCount { get; init; } = 1_000;

    /// <summary>Maximum serialized state size in UTF-8 bytes.</summary>
    public int MaximumStateBytes { get; init; } = OneMebibyte;

    /// <summary>Maximum serialized configuration size per node in UTF-8 bytes.</summary>
    public int MaximumNodeConfigurationBytes { get; init; } = 256 * 1_024;

    /// <summary>Maximum JSON nesting depth.</summary>
    public int MaximumJsonDepth { get; init; } = 64;

    internal void EnsureValid()
    {
        if (MaximumDefinitionBytes <= 0 || MaximumNodeCount <= 0 || MaximumStateBytes <= 0 ||
            MaximumNodeConfigurationBytes <= 0 || MaximumJsonDepth <= 0)
        {
            throw new InvalidOperationException("Workflow limits must all be strictly positive.");
        }
    }
}


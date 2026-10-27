using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Core.Serialization;

public interface IWorkflowSerializer
{
    CanonicalJson Serialize<T>(T value) where T : class;
    T Deserialize<T>(CanonicalJson value) where T : class;
    bool IsValidState(CanonicalJson value);
}

/// <summary>Operational budgets; the persistent DTO naming/conversion contract remains immutable.</summary>
public sealed class WorkflowSerializationOptions
{
    public int MaximumStateBytes { get; set; } = WorkflowDefinitionLimits.Default.MaximumStateBytes;
    public int MaximumDepth { get; set; } = WorkflowDefinitionLimits.Default.MaximumJsonDepth;
    public void Validate()
    {
        if (MaximumStateBytes is < 1 || MaximumStateBytes > WorkflowDefinitionLimits.Default.MaximumStateBytes ||
            MaximumDepth is < 1 || MaximumDepth > WorkflowDefinitionLimits.Default.MaximumJsonDepth)
            throw new InvalidOperationException("Serialization budgets must be positive and within the engine state/depth limits.");
    }
}

public sealed class WorkflowSerializer : IWorkflowSerializer
{
    private readonly int _maximumBytes;
    private readonly int _maximumDepth;
    public WorkflowSerializer(WorkflowSerializationOptions? options = null)
    {
        options ??= new(); options.Validate();
        _maximumBytes = options.MaximumStateBytes; _maximumDepth = options.MaximumDepth;
    }
    public CanonicalJson Serialize<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return CanonicalJson.CreateObject(JsonSerializer.Serialize(value, WorkflowJson.Options), _maximumBytes, _maximumDepth);
    }
    public T Deserialize<T>(CanonicalJson value) where T : class => value.Deserialize<T>();
    public bool IsValidState(CanonicalJson value)
    {
        try { CanonicalJson.CreateObject(value.CanonicalText, _maximumBytes, _maximumDepth); return true; }
        catch (FormatException) { return false; }
    }
}

using System.Collections.Immutable;

namespace EnterpriseWorkflow.Core.Model;

/// <summary>
/// Structural role of a node in the sequential MVP graph.
/// </summary>
public enum WorkflowNodeRole
{
    /// <summary>The unique graph entry point.</summary>
    Start,

    /// <summary>A named service executor.</summary>
    Service,

    /// <summary>A named evaluator with exclusive outcomes.</summary>
    Decision,

    /// <summary>A terminal graph node.</summary>
    End,
}

/// <summary>
/// An inspectable node before validation and canonicalization.
/// </summary>
public sealed record WorkflowNodeDraft(
    string Id,
    string TypeId,
    int TypeVersion,
    WorkflowNodeRole Role,
    string? HandlerId,
    int? HandlerVersion,
    string ConfigurationJson,
    ImmutableArray<string> DeclaredOutcomes,
    string? DisplayLabel);

/// <summary>
/// An inspectable transition before validation and canonicalization.
/// </summary>
public sealed record WorkflowTransitionDraft(
    string SourceId,
    string TargetId,
    string? Outcome,
    bool IsDefault);

/// <summary>
/// An inspectable immutable-artifact reference before validation.
/// </summary>
public sealed record WorkflowArtifactReferenceDraft(string Id, string Version, string Sha256);

/// <summary>
/// An immutable snapshot produced by the authoring SDK, valid or incomplete.
/// </summary>
public sealed class WorkflowDraft
{
    /// <summary>Initializes a draft snapshot.</summary>
    public WorkflowDraft(
        string definitionId,
        int definitionVersion,
        string? displayLabel,
        IEnumerable<WorkflowArtifactReferenceDraft> artifacts,
        IEnumerable<WorkflowNodeDraft> nodes,
        IEnumerable<WorkflowTransitionDraft> transitions)
    {
        DefinitionId = definitionId ?? string.Empty;
        DefinitionVersion = definitionVersion;
        DisplayLabel = displayLabel;
        Artifacts = artifacts.ToImmutableArray();
        Nodes = nodes.ToImmutableArray();
        Transitions = transitions.ToImmutableArray();
    }

    /// <summary>Gets the unvalidated definition identifier.</summary>
    public string DefinitionId { get; }

    /// <summary>Gets the unvalidated definition version.</summary>
    public int DefinitionVersion { get; }

    /// <summary>Gets the display label.</summary>
    public string? DisplayLabel { get; }

    /// <summary>Gets immutable artifact references required by this definition.</summary>
    public ImmutableArray<WorkflowArtifactReferenceDraft> Artifacts { get; }

    /// <summary>Gets the immutable node snapshot.</summary>
    public ImmutableArray<WorkflowNodeDraft> Nodes { get; }

    /// <summary>Gets the immutable transition snapshot.</summary>
    public ImmutableArray<WorkflowTransitionDraft> Transitions { get; }
}

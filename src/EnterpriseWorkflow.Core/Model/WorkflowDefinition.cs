using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Core.Model;

/// <summary>
/// A named and versioned executable or evaluator reference.
/// </summary>
public sealed record WorkflowHandlerReference(TechnicalId Id, int Version);

/// <summary>A durable form reference rendered by an application layer.</summary>
public sealed record WorkflowFormReference(TechnicalId Id, int Version);

/// <summary>An action exposed by a human task and the graph outcome it selects.</summary>
public sealed record HumanTaskAction(TechnicalId ActionId, TechnicalId Outcome);

/// <summary>Requires an actor distinct from the author of an upstream action.</summary>
public sealed record HumanTaskActorConstraint(TechnicalId NodeId, TechnicalId ActionId);

/// <summary>Validated, versioned contract of a durable human task.</summary>
public sealed record HumanTaskDefinition(
    HumanTaskKind Kind,
    HumanTaskAssignmentMode AssignmentMode,
    WorkflowFormReference Form,
    WorkflowHandlerReference CompletionHandler,
    ImmutableArray<HumanTaskAction> Actions,
    bool AllowInitiator,
    ImmutableArray<HumanTaskActorConstraint> DistinctFrom);

/// <summary>Validated fixed duration used to compute a deadline from the store clock.</summary>
public sealed record TimerDefinition(TimeSpan Delay);

/// <summary>
/// A validated node in a canonical definition.
/// </summary>
public sealed record WorkflowNode(
    TechnicalId Id,
    NodeTypeReference Type,
    WorkflowNodeRole Role,
    WorkflowHandlerReference? Handler,
    CanonicalJson Configuration,
    ImmutableArray<TechnicalId> DeclaredOutcomes,
    string? DisplayLabel,
    HumanTaskDefinition? HumanTask,
    TimerDefinition? Timer);

/// <summary>
/// A validated transition in a canonical definition.
/// </summary>
public sealed record WorkflowTransition(
    TechnicalId SourceId,
    TechnicalId TargetId,
    TechnicalId? Outcome,
    bool IsDefault);

/// <summary>
/// A node type and its contract version.
/// </summary>
public sealed record NodeTypeReference(TechnicalId Id, int Version);

/// <summary>
/// Identifies an immutable module or other code artifact separately from the definition hash.
/// </summary>
public sealed record WorkflowArtifactReference(TechnicalId Id, string Version, string Sha256);

/// <summary>
/// An immutable, normalized and publishable workflow definition.
/// </summary>
public sealed class WorkflowDefinition
{
    internal WorkflowDefinition(
        TechnicalId id,
        int version,
        string? displayLabel,
        ImmutableArray<WorkflowArtifactReference> artifacts,
        ImmutableArray<WorkflowNode> nodes,
        ImmutableArray<WorkflowTransition> transitions,
        string canonicalJson,
        string sha256)
    {
        Id = id;
        Version = version;
        DisplayLabel = displayLabel;
        Artifacts = artifacts;
        Nodes = nodes;
        Transitions = transitions;
        CanonicalJson = canonicalJson;
        Sha256 = sha256;
    }

    /// <summary>Gets the current canonical serialization schema version.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Gets the stable definition identifier.</summary>
    public TechnicalId Id { get; }

    /// <summary>Gets the immutable positive definition version.</summary>
    public int Version { get; }

    /// <summary>Gets the optional display label.</summary>
    public string? DisplayLabel { get; }

    /// <summary>Gets immutable code artifacts ordered by identifier and version.</summary>
    public ImmutableArray<WorkflowArtifactReference> Artifacts { get; }

    /// <summary>Gets nodes ordered by their ordinal identifiers.</summary>
    public ImmutableArray<WorkflowNode> Nodes { get; }

    /// <summary>Gets transitions in canonical ordinal order.</summary>
    public ImmutableArray<WorkflowTransition> Transitions { get; }

    /// <summary>Gets the canonical UTF-8 JSON represented as text.</summary>
    public string CanonicalJson { get; }

    /// <summary>Gets the lowercase SHA-256 of the canonical UTF-8 JSON.</summary>
    public string Sha256 { get; }
}

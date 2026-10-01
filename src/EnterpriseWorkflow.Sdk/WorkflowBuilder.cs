using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Validation;

namespace EnterpriseWorkflow.Sdk;

/// <summary>
/// Minimal C# DSL that creates inspectable workflow drafts without publishing them.
/// </summary>
public sealed class WorkflowBuilder
{
    private readonly string _definitionId;
    private readonly int _definitionVersion;
    private readonly List<WorkflowNodeDraft> _nodes = [];
    private readonly List<WorkflowTransitionDraft> _transitions = [];
    private readonly List<WorkflowArtifactReferenceDraft> _artifacts = [];
    private string? _displayLabel;

    private WorkflowBuilder(string definitionId, int definitionVersion)
    {
        _definitionId = definitionId;
        _definitionVersion = definitionVersion;
    }

    /// <summary>Begins an inspectable workflow draft.</summary>
    public static WorkflowBuilder Create(string definitionId, int definitionVersion) =>
        new(definitionId, definitionVersion);

    /// <summary>Sets a display-only label.</summary>
    public WorkflowBuilder WithDisplayLabel(string? displayLabel)
    {
        _displayLabel = displayLabel;
        return this;
    }

    /// <summary>References an immutable module or code artifact required by this definition.</summary>
    public WorkflowBuilder RequiresArtifact(string id, string version, string sha256)
    {
        _artifacts.Add(new WorkflowArtifactReferenceDraft(id, version, sha256));
        return this;
    }

    /// <summary>Adds the start node.</summary>
    public WorkflowBuilder Start(string id, string configurationJson = "{}", string? displayLabel = null) =>
        AddNode(id, "core.start", 1, WorkflowNodeRole.Start, null, null, configurationJson, [], displayLabel);

    /// <summary>Adds a service node that references a named executor.</summary>
    public WorkflowBuilder Service(
        string id,
        string executorId,
        int executorVersion = 1,
        string configurationJson = "{}",
        string? displayLabel = null) =>
        AddNode(
            id,
            "core.service",
            1,
            WorkflowNodeRole.Service,
            executorId,
            executorVersion,
            configurationJson,
            [],
            displayLabel);

    /// <summary>Adds an exclusive decision that references a named evaluator.</summary>
    public WorkflowBuilder Decision(
        string id,
        string evaluatorId,
        IEnumerable<string> outcomes,
        int evaluatorVersion = 1,
        string configurationJson = "{}",
        string? displayLabel = null) =>
        AddNode(
            id,
            "core.decision",
            1,
            WorkflowNodeRole.Decision,
            evaluatorId,
            evaluatorVersion,
            configurationJson,
            outcomes,
            displayLabel);

    /// <summary>Adds an end node.</summary>
    public WorkflowBuilder End(string id, string configurationJson = "{}", string? displayLabel = null) =>
        AddNode(id, "core.end", 1, WorkflowNodeRole.End, null, null, configurationJson, [], displayLabel);

    /// <summary>Adds a catalog-defined node while preserving one of the bounded L2 flow roles.</summary>
    public WorkflowBuilder CustomNode(
        string id,
        string typeId,
        int typeVersion,
        WorkflowNodeRole role,
        string? handlerId,
        int? handlerVersion,
        string configurationJson,
        IEnumerable<string>? outcomes = null,
        string? displayLabel = null) =>
        AddNode(
            id,
            typeId,
            typeVersion,
            role,
            handlerId,
            handlerVersion,
            configurationJson,
            outcomes ?? [],
            displayLabel);

    /// <summary>Adds the single normal transition of a start or service node.</summary>
    public WorkflowBuilder Then(string sourceId, string targetId)
    {
        _transitions.Add(new WorkflowTransitionDraft(sourceId, targetId, null, IsDefault: false));
        return this;
    }

    /// <summary>Adds a named exclusive decision branch.</summary>
    public WorkflowBuilder On(string decisionId, string outcome, string targetId)
    {
        _transitions.Add(new WorkflowTransitionDraft(decisionId, targetId, outcome, IsDefault: false));
        return this;
    }

    /// <summary>Adds an explicit decision default branch.</summary>
    public WorkflowBuilder Otherwise(string decisionId, string targetId)
    {
        _transitions.Add(new WorkflowTransitionDraft(decisionId, targetId, null, IsDefault: true));
        return this;
    }

    /// <summary>Creates an immutable snapshot that remains inspectable when incomplete.</summary>
    public WorkflowDraft BuildDraft() =>
        new(_definitionId, _definitionVersion, _displayLabel, _artifacts, _nodes, _transitions);

    /// <summary>Explicitly validates and canonicalizes the current snapshot.</summary>
    public WorkflowCompilationResult Validate(
        NodeTypeCatalog? catalog = null,
        WorkflowDefinitionLimits? limits = null) =>
        WorkflowDefinitionCompiler.Compile(BuildDraft(), catalog, limits);

    private WorkflowBuilder AddNode(
        string id,
        string typeId,
        int typeVersion,
        WorkflowNodeRole role,
        string? handlerId,
        int? handlerVersion,
        string configurationJson,
        IEnumerable<string> outcomes,
        string? displayLabel)
    {
        _nodes.Add(new WorkflowNodeDraft(
            id,
            typeId,
            typeVersion,
            role,
            handlerId,
            handlerVersion,
            configurationJson,
            outcomes.ToImmutableArray(),
            displayLabel));
        return this;
    }
}

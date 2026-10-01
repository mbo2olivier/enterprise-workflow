using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Diagnostics;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Serialization;

namespace EnterpriseWorkflow.Core.Validation;

/// <summary>
/// Validates an inspectable draft and produces its immutable canonical representation.
/// </summary>
public static class WorkflowDefinitionCompiler
{
    /// <summary>
    /// Performs structural validation followed by catalog/configuration validation.
    /// </summary>
    public static WorkflowCompilationResult Compile(
        WorkflowDraft draft,
        NodeTypeCatalog? catalog = null,
        WorkflowDefinitionLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        catalog ??= NodeTypeCatalog.BuiltIns;
        limits ??= WorkflowDefinitionLimits.Default;
        limits.EnsureValid();

        var diagnostics = new List<WorkflowDiagnostic>();
        var definitionIdIsValid = TechnicalId.IsValid(draft.DefinitionId);
        if (!definitionIdIsValid)
        {
            AddError(diagnostics, "EW1001", "definition.id", "The definition identifier is invalid.");
        }

        if (draft.DefinitionVersion <= 0)
        {
            AddError(diagnostics, "EW1002", "definition.version", "The definition version must be strictly positive.");
        }

        if (draft.Nodes.Length > limits.MaximumNodeCount)
        {
            AddError(
                diagnostics,
                "EW1003",
                "definition.nodes",
                $"The definition contains {draft.Nodes.Length} nodes; the limit is {limits.MaximumNodeCount}.");
        }

        var artifacts = ValidateArtifacts(draft.Artifacts, diagnostics);
        var nodes = ValidateNodes(draft.Nodes, catalog, limits, diagnostics);
        var transitions = ValidateTransitions(draft.Transitions, diagnostics);

        ValidateGraph(nodes, transitions, diagnostics);

        if (HasErrors(diagnostics) || !definitionIdIsValid)
        {
            return new WorkflowCompilationResult(null, diagnostics);
        }

        var canonicalJson = WorkflowDefinitionSerializer.SerializeCanonical(
            draft.DefinitionId,
            draft.DefinitionVersion,
            draft.DisplayLabel,
            artifacts,
            nodes,
            transitions);
        var canonicalByteCount = Encoding.UTF8.GetByteCount(canonicalJson);
        var receivedByteCount = WorkflowDefinitionSerializer.GetReceivedByteCount(
            draft.DefinitionId,
            draft.DefinitionVersion,
            draft.DisplayLabel,
            artifacts,
            nodes,
            transitions);

        if (receivedByteCount > limits.MaximumDefinitionBytes || canonicalByteCount > limits.MaximumDefinitionBytes)
        {
            AddError(
                diagnostics,
                "EW1501",
                "definition",
                $"The definition uses {receivedByteCount} received and {canonicalByteCount} canonical UTF-8 bytes; the limit is {limits.MaximumDefinitionBytes}.");
            return new WorkflowCompilationResult(null, diagnostics);
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
        var definition = new WorkflowDefinition(
            new TechnicalId(draft.DefinitionId),
            draft.DefinitionVersion,
            draft.DisplayLabel,
            artifacts
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                .ThenBy(item => item.Version, StringComparer.Ordinal)
                .ToImmutableArray(),
            nodes.OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToImmutableArray(),
            transitions
                .OrderBy(item => item.SourceId.Value, StringComparer.Ordinal)
                .ThenBy(item => item.IsDefault)
                .ThenBy(item => item.Outcome?.Value, StringComparer.Ordinal)
                .ThenBy(item => item.TargetId.Value, StringComparer.Ordinal)
                .ToImmutableArray(),
            canonicalJson,
            hash);

        return new WorkflowCompilationResult(definition, diagnostics);
    }

    private static ImmutableArray<WorkflowArtifactReference> ValidateArtifacts(
        ImmutableArray<WorkflowArtifactReferenceDraft> drafts,
        List<WorkflowDiagnostic> diagnostics)
    {
        var artifacts = ImmutableArray.CreateBuilder<WorkflowArtifactReference>();
        var seen = new Dictionary<(string Id, string Version), string>();
        for (var index = 0; index < drafts.Length; index++)
        {
            var draft = drafts[index];
            var location = $"artifacts[{index}]";
            if (!TechnicalId.IsValid(draft.Id) || string.IsNullOrWhiteSpace(draft.Version) || !IsSha256(draft.Sha256))
            {
                AddError(diagnostics, "EW1010", location, "An artifact requires a valid identifier, non-empty version, and 64-character hexadecimal SHA-256.");
                continue;
            }

            var key = (draft.Id, draft.Version);
            var normalizedHash = draft.Sha256.ToLowerInvariant();
            if (seen.TryGetValue(key, out var existingHash))
            {
                var message = string.Equals(existingHash, normalizedHash, StringComparison.Ordinal)
                    ? "The artifact reference is duplicated."
                    : "The same artifact identifier and version reference different hashes.";
                AddError(diagnostics, "EW1011", location, message);
                continue;
            }

            seen.Add(key, normalizedHash);
            artifacts.Add(new WorkflowArtifactReference(new TechnicalId(draft.Id), draft.Version, normalizedHash));
        }

        return artifacts.ToImmutable();
    }

    private static ImmutableArray<WorkflowNode> ValidateNodes(
        ImmutableArray<WorkflowNodeDraft> drafts,
        NodeTypeCatalog catalog,
        WorkflowDefinitionLimits limits,
        List<WorkflowDiagnostic> diagnostics)
    {
        var nodes = ImmutableArray.CreateBuilder<WorkflowNode>();
        var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < drafts.Length; index++)
        {
            var draft = drafts[index];
            var location = $"nodes[{index}]";
            var nodeIsValid = true;

            if (!TechnicalId.IsValid(draft.Id))
            {
                AddError(diagnostics, "EW1101", $"{location}.id", "The node identifier is invalid.");
                nodeIsValid = false;
            }
            else if (!seenNodeIds.Add(draft.Id))
            {
                AddError(diagnostics, "EW1102", $"{location}.id", $"The node identifier '{draft.Id}' is duplicated.");
                nodeIsValid = false;
            }

            if (!TechnicalId.IsValid(draft.TypeId) || draft.TypeVersion <= 0)
            {
                AddError(diagnostics, "EW1103", $"{location}.type", "The node type identifier or version is invalid.");
                nodeIsValid = false;
            }

            WorkflowHandlerReference? handler = null;
            var requiresHandler = draft.Role is WorkflowNodeRole.Service or WorkflowNodeRole.Decision;
            if (requiresHandler)
            {
                if (!TechnicalId.IsValid(draft.HandlerId) || draft.HandlerVersion is null or <= 0)
                {
                    AddError(diagnostics, "EW1104", $"{location}.handler", "Service and decision nodes require a named, positively versioned handler.");
                    nodeIsValid = false;
                }
                else
                {
                    handler = new WorkflowHandlerReference(new TechnicalId(draft.HandlerId!), draft.HandlerVersion.Value);
                }
            }
            else if (draft.HandlerId is not null || draft.HandlerVersion is not null)
            {
                AddError(diagnostics, "EW1104", $"{location}.handler", "Start and end nodes cannot declare a handler.");
                nodeIsValid = false;
            }

            var outcomes = ImmutableArray.CreateBuilder<TechnicalId>();
            var seenOutcomes = new HashSet<string>(StringComparer.Ordinal);
            for (var outcomeIndex = 0; outcomeIndex < draft.DeclaredOutcomes.Length; outcomeIndex++)
            {
                var outcome = draft.DeclaredOutcomes[outcomeIndex];
                if (!TechnicalId.IsValid(outcome))
                {
                    AddError(diagnostics, "EW1105", $"{location}.outcomes[{outcomeIndex}]", "The outcome identifier is invalid.");
                    nodeIsValid = false;
                }
                else if (!seenOutcomes.Add(outcome))
                {
                    AddError(diagnostics, "EW1106", $"{location}.outcomes[{outcomeIndex}]", $"The outcome '{outcome}' is duplicated.");
                    nodeIsValid = false;
                }
                else
                {
                    outcomes.Add(new TechnicalId(outcome));
                }
            }

            if (draft.Role is WorkflowNodeRole.Decision && outcomes.Count == 0)
            {
                AddError(diagnostics, "EW1107", $"{location}.outcomes", "A decision must declare at least one named outcome.");
                nodeIsValid = false;
            }
            else if (draft.Role is not WorkflowNodeRole.Decision && outcomes.Count != 0)
            {
                AddError(diagnostics, "EW1107", $"{location}.outcomes", "Only decision nodes can declare outcomes.");
                nodeIsValid = false;
            }

            if (!CanonicalJson.TryCreate(
                    draft.ConfigurationJson,
                    limits.MaximumNodeConfigurationBytes,
                    limits.MaximumJsonDepth,
                    out var configuration,
                    out var jsonCode,
                    out var jsonMessage))
            {
                AddError(diagnostics, jsonCode, $"{location}.configuration", jsonMessage);
                nodeIsValid = false;
            }

            if (!nodeIsValid || configuration is null)
            {
                continue;
            }

            var type = new NodeTypeReference(new TechnicalId(draft.TypeId), draft.TypeVersion);
            if (!catalog.TryGet(type, out var descriptor) || descriptor is null)
            {
                AddError(diagnostics, "EW1401", $"{location}.type", $"Node type '{draft.TypeId}' version {draft.TypeVersion} is not present in the catalog.");
            }
            else if (descriptor.Role != draft.Role)
            {
                AddError(diagnostics, "EW1402", $"{location}.role", $"The catalog describes this type as {descriptor.Role}, not {draft.Role}.");
            }
            else if (descriptor.ConfigurationValidator is not null)
            {
                diagnostics.AddRange(descriptor.ConfigurationValidator.Validate(configuration, location));
            }

            nodes.Add(new WorkflowNode(
                new TechnicalId(draft.Id),
                type,
                draft.Role,
                handler,
                configuration,
                outcomes.ToImmutable(),
                draft.DisplayLabel));
        }

        return nodes.ToImmutable();
    }

    private static ImmutableArray<WorkflowTransition> ValidateTransitions(
        ImmutableArray<WorkflowTransitionDraft> drafts,
        List<WorkflowDiagnostic> diagnostics)
    {
        var transitions = ImmutableArray.CreateBuilder<WorkflowTransition>();

        for (var index = 0; index < drafts.Length; index++)
        {
            var draft = drafts[index];
            var location = $"transitions[{index}]";
            var isValid = true;
            if (!TechnicalId.IsValid(draft.SourceId))
            {
                AddError(diagnostics, "EW1301", $"{location}.source", "The transition source identifier is invalid.");
                isValid = false;
            }

            if (!TechnicalId.IsValid(draft.TargetId))
            {
                AddError(diagnostics, "EW1301", $"{location}.target", "The transition target identifier is invalid.");
                isValid = false;
            }

            TechnicalId? outcome = null;
            if (draft.Outcome is not null)
            {
                if (!TechnicalId.IsValid(draft.Outcome))
                {
                    AddError(diagnostics, "EW1301", $"{location}.outcome", "The transition outcome identifier is invalid.");
                    isValid = false;
                }
                else
                {
                    outcome = new TechnicalId(draft.Outcome);
                }
            }

            if (draft.IsDefault && outcome is not null)
            {
                AddError(diagnostics, "EW1302", location, "A default branch cannot also name an outcome.");
                isValid = false;
            }

            if (isValid)
            {
                transitions.Add(new WorkflowTransition(
                    new TechnicalId(draft.SourceId),
                    new TechnicalId(draft.TargetId),
                    outcome,
                    draft.IsDefault));
            }
        }

        return transitions.ToImmutable();
    }

    private static void ValidateGraph(
        ImmutableArray<WorkflowNode> nodes,
        ImmutableArray<WorkflowTransition> transitions,
        List<WorkflowDiagnostic> diagnostics)
    {
        var nodesById = nodes.ToDictionary(item => item.Id.Value, StringComparer.Ordinal);
        var starts = nodes.Where(item => item.Role is WorkflowNodeRole.Start).ToArray();
        var ends = nodes.Where(item => item.Role is WorkflowNodeRole.End).ToArray();
        if (starts.Length != 1)
        {
            AddError(diagnostics, "EW1310", "definition.nodes", $"Exactly one start node is required; found {starts.Length}.");
        }

        if (ends.Length == 0)
        {
            AddError(diagnostics, "EW1311", "definition.nodes", "At least one end node is required.");
        }

        var validTransitions = new List<WorkflowTransition>();
        foreach (var transition in transitions)
        {
            var location = $"transition:{transition.SourceId.Value}";
            if (!nodesById.ContainsKey(transition.SourceId.Value))
            {
                AddError(diagnostics, "EW1312", location, $"Transition source '{transition.SourceId.Value}' does not exist.");
                continue;
            }

            if (!nodesById.ContainsKey(transition.TargetId.Value))
            {
                AddError(diagnostics, "EW1313", location, $"Transition target '{transition.TargetId.Value}' does not exist.");
                continue;
            }

            validTransitions.Add(transition);
        }

        var outgoing = validTransitions
            .GroupBy(item => item.SourceId.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            var edges = outgoing.GetValueOrDefault(node.Id.Value) ?? [];
            ValidateOutgoing(node, edges, diagnostics);
        }

        if (starts.Length != 1)
        {
            return;
        }

        var reachable = Traverse(starts[0].Id.Value, outgoing);
        foreach (var node in nodes.Where(item => !reachable.Contains(item.Id.Value)))
        {
            AddError(diagnostics, "EW1320", $"node:{node.Id.Value}", "The node is unreachable from the start node.");
        }

        if (ContainsCycle(reachable, outgoing))
        {
            AddError(diagnostics, "EW1321", "definition.transitions", "The reachable graph contains a cycle.");
        }

        var canReachEnd = FindNodesThatCanReachAnEnd(nodes, validTransitions);
        foreach (var nodeId in reachable.Where(item => !canReachEnd.Contains(item)).Order(StringComparer.Ordinal))
        {
            AddError(diagnostics, "EW1322", $"node:{nodeId}", "No path from this node reaches an end node.");
        }
    }

    private static void ValidateOutgoing(
        WorkflowNode node,
        WorkflowTransition[] edges,
        List<WorkflowDiagnostic> diagnostics)
    {
        var location = $"node:{node.Id.Value}";
        if (node.Role is WorkflowNodeRole.End)
        {
            if (edges.Length != 0)
            {
                AddError(diagnostics, "EW1314", location, "An end node cannot have outgoing transitions.");
            }

            return;
        }

        if (node.Role is WorkflowNodeRole.Start or WorkflowNodeRole.Service)
        {
            if (edges.Length != 1 || edges.Any(item => item.IsDefault || item.Outcome is not null))
            {
                AddError(diagnostics, "EW1315", location, "Start and service nodes require exactly one normal transition.");
            }

            return;
        }

        var duplicateBranches = edges
            .GroupBy(item => item.IsDefault ? "<default>" : item.Outcome?.Value ?? "<normal>", StringComparer.Ordinal)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicateBranches)
        {
            AddError(diagnostics, "EW1316", location, $"Decision branch '{duplicate.Key}' is ambiguous.");
        }

        if (edges.Count(item => item.IsDefault) > 1)
        {
            AddError(diagnostics, "EW1317", location, "A decision can declare at most one default branch.");
        }

        var declared = node.DeclaredOutcomes.Select(item => item.Value).ToHashSet(StringComparer.Ordinal);
        var namedEdges = edges.Where(item => !item.IsDefault && item.Outcome is not null).ToArray();
        foreach (var edge in edges.Where(item => !item.IsDefault && item.Outcome is null))
        {
            AddError(diagnostics, "EW1318", location, $"Decision transition to '{edge.TargetId.Value}' must name an outcome or be explicitly default.");
        }

        foreach (var edge in namedEdges.Where(item => !declared.Contains(item.Outcome!.Value.Value)))
        {
            AddError(diagnostics, "EW1318", location, $"Outcome '{edge.Outcome!.Value.Value}' is not declared by the decision.");
        }

        var mapped = namedEdges.Select(item => item.Outcome!.Value.Value).ToHashSet(StringComparer.Ordinal);
        if (!edges.Any(item => item.IsDefault))
        {
            foreach (var missing in declared.Where(item => !mapped.Contains(item)).Order(StringComparer.Ordinal))
            {
                AddError(diagnostics, "EW1319", location, $"Declared outcome '{missing}' is not connected and no default branch exists.");
            }
        }
    }

    private static HashSet<string> Traverse(
        string start,
        IReadOnlyDictionary<string, WorkflowTransition[]> outgoing)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(start);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var edge in outgoing.GetValueOrDefault(current) ?? [])
            {
                pending.Push(edge.TargetId.Value);
            }
        }

        return visited;
    }

    private static bool ContainsCycle(
        HashSet<string> reachable,
        IReadOnlyDictionary<string, WorkflowTransition[]> outgoing)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string nodeId)
        {
            if (visiting.Contains(nodeId))
            {
                return true;
            }

            if (!visited.Add(nodeId))
            {
                return false;
            }

            visiting.Add(nodeId);
            foreach (var edge in outgoing.GetValueOrDefault(nodeId) ?? [])
            {
                if (reachable.Contains(edge.TargetId.Value) && Visit(edge.TargetId.Value))
                {
                    return true;
                }
            }

            visiting.Remove(nodeId);
            return false;
        }

        return reachable.Any(Visit);
    }

    private static HashSet<string> FindNodesThatCanReachAnEnd(
        ImmutableArray<WorkflowNode> nodes,
        IEnumerable<WorkflowTransition> transitions)
    {
        var incoming = transitions
            .GroupBy(item => item.TargetId.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.SourceId.Value).ToArray(), StringComparer.Ordinal);
        var canReachEnd = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(nodes
            .Where(item => item.Role is WorkflowNodeRole.End)
            .Select(item => item.Id.Value));
        while (pending.TryPop(out var current))
        {
            if (!canReachEnd.Add(current))
            {
                continue;
            }

            foreach (var source in incoming.GetValueOrDefault(current) ?? [])
            {
                pending.Push(source);
            }
        }

        return canReachEnd;
    }

    private static bool HasErrors(IEnumerable<WorkflowDiagnostic> diagnostics) =>
        diagnostics.Any(item => item.Severity is WorkflowDiagnosticSeverity.Error);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static void AddError(
        List<WorkflowDiagnostic> diagnostics,
        string code,
        string location,
        string message) =>
        diagnostics.Add(new WorkflowDiagnostic(code, WorkflowDiagnosticSeverity.Error, location, message));
}

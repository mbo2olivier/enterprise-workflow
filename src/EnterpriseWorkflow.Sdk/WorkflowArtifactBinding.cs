using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Validation;

namespace EnterpriseWorkflow.Sdk;

/// <summary>Binds the final artifact hash to a frozen authoring snapshot, never to mutable builders.</summary>
internal static class WorkflowArtifactBinding
{
    internal static WorkflowDefinition Bind(WorkflowDefinition template, WorkflowArtifactReference placeholder, WorkflowArtifactReference owner)
    {
        if (placeholder.Id != owner.Id || placeholder.Version != owner.Version)
            throw new InvalidOperationException("The supplied artifact does not match the declared module release.");
        var draft = new WorkflowDraft(template.Id.Value, template.Version, template.DisplayLabel,
            template.Artifacts.Select(artifact => artifact == placeholder ? owner : artifact)
                .Select(artifact => new WorkflowArtifactReferenceDraft(artifact.Id.Value, artifact.Version, artifact.Sha256)),
            template.Nodes.Select(node => new WorkflowNodeDraft(node.Id.Value, node.Type.Id.Value, node.Type.Version,
                node.Role, node.Handler?.Id.Value, node.Handler?.Version, node.Configuration.CanonicalText,
                node.DeclaredOutcomes.Select(outcome => outcome.Value).ToImmutableArray(), node.DisplayLabel,
                node.HumanTask is not { } task ? null : new HumanTaskDefinitionDraft(task.Kind, task.AssignmentMode,
                    task.Form.Id.Value, task.Form.Version, task.CompletionHandler.Id.Value, task.CompletionHandler.Version,
                    task.Actions.Select(action => new HumanTaskActionDraft(action.ActionId.Value, action.Outcome.Value)).ToImmutableArray(),
                    task.AllowInitiator, task.DistinctFrom.Select(constraint => new HumanTaskActorConstraintDraft(constraint.NodeId.Value, constraint.ActionId.Value)).ToImmutableArray()),
                node.Timer is not { } timer ? null : new TimerDefinitionDraft(timer.Delay))),
            template.Transitions.Select(edge => new WorkflowTransitionDraft(edge.SourceId.Value, edge.TargetId.Value, edge.Outcome?.Value, edge.IsDefault)));
        var compiled = WorkflowDefinitionCompiler.Compile(draft);
        return compiled.Definition ?? throw new InvalidOperationException(string.Join("; ", compiled.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }
}

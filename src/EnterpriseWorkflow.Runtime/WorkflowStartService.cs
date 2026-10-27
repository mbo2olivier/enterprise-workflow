using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Outcome returned by the authenticated workflow-start boundary.</summary>
public sealed record WorkflowStartResult(
    bool Succeeded,
    bool WasReplay,
    WorkflowInstanceId? InstanceId,
    long? InstanceRevision,
    string? ErrorCode = null, IReadOnlyList<WorkflowValidationError>? Errors = null);

/// <summary>Authorizes a start and validates every prepared designated assignment before persistence.</summary>
public interface IWorkflowStartService
{
    ValueTask<WorkflowStartResult> StartAsync(
        WorkflowDefinition definition,
        StartInstanceCommand command,
        CancellationToken cancellationToken);
}

internal sealed class WorkflowStartService(
    IWorkflowStore store,
    IWorkflowAuthorizationService authorization,
    IWorkflowStateValidationService? stateValidation = null, EnterpriseWorkflow.Core.Serialization.IWorkflowSerializer? serializer = null) : IWorkflowStartService
{
    public async ValueTask<WorkflowStartResult> StartAsync(
        WorkflowDefinition definition,
        StartInstanceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(command);
        if (!(serializer ?? new EnterpriseWorkflow.Core.Serialization.WorkflowSerializer()).IsValidState(command.InitialState.Value)) return new(false, false, null, null, "runtime.state-invalid");
        if (command.Definition.DefinitionId != definition.Id ||
            command.Definition.Version != definition.Version ||
            !string.Equals(command.Definition.Sha256, definition.Sha256, StringComparison.Ordinal))
            return Denied("runtime.start-definition-mismatch");

        var initiator = ToIdentity(command.Scope.Actor);
        if (!await IsAllowedAsync(initiator, definition, null, WorkflowActions.Start, cancellationToken).ConfigureAwait(false))
            return Denied("security.workflow-start-denied");

        var startNode = definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start);
        if (WorkflowStagePolicy.Read(startNode.Configuration) is not null && command.DesignatedAssignments is { Count: > 0 })
            return Denied("runtime.start-assignment-not-declared");

        foreach (var assignment in command.DesignatedAssignments ?? [])
        {
            var node = definition.Nodes.SingleOrDefault(candidate => candidate.Id == assignment.NodeId);
            if (node?.HumanTask is not { AssignmentMode: HumanTaskAssignmentMode.DesignatedIdentity })
                return Denied("runtime.designated-assignment-node-invalid");
            if (!await HasAnyTaskActionAsync(ToIdentity(assignment.Assignee), definition, node, cancellationToken)
                    .ConfigureAwait(false))
                return Denied("security.task-assignee-ineligible");
        }

        var stateContract = WorkflowStateContract.From(definition);
        if (stateContract is not null)
        {
            if (stateContract.Version != command.InitialState.SchemaVersion) return Denied("runtime.state-invalid");
            var contractErrors = stateContract.ValidateErrors(command.InitialState.Value);
            if (contractErrors.Count > 0) return Denied("runtime.state-invalid") with { Errors = contractErrors };
        }
        if ((stateContract?.Validators?.Length ?? 0) > 0)
        {
            if (stateValidation is null) return Denied("runtime.state-invalid");
            var errors = await stateValidation.ValidateErrorsAsync(definition, new(command.InitialState, "start", null, []), cancellationToken);
            if (errors.Count > 0) return Denied("runtime.validation-failed") with { Errors = errors };
        }
        var persisted = await store.StartInstanceAsync(command, cancellationToken).ConfigureAwait(false);
        return persisted.Outcome switch
        {
            StoreOutcome.Succeeded when persisted.Value is not null =>
                new(true, false, persisted.Value.InstanceId, persisted.Value.Revision),
            StoreOutcome.Idempotent when persisted.Value is not null =>
                new(true, true, persisted.Value.InstanceId, persisted.Value.Revision),
            _ => Denied(persisted.ErrorCode ?? "runtime.workflow-start-failed"),
        };
    }

    private async ValueTask<bool> HasAnyTaskActionAsync(
        IdentityReference identity,
        WorkflowDefinition definition,
        WorkflowNode node,
        CancellationToken cancellationToken)
    {
        foreach (var action in node.HumanTask!.Actions)
        {
            if (await IsAllowedAsync(identity, definition, node.Id.Value,
                    new WorkflowActionId(action.ActionId.Value), cancellationToken).ConfigureAwait(false))
                return true;
        }
        return false;
    }

    private async ValueTask<bool> IsAllowedAsync(
        IdentityReference identity,
        WorkflowDefinition definition,
        string? nodeId,
        WorkflowActionId action,
        CancellationToken cancellationToken)
    {
        var decision = await authorization.AuthorizeAsync(new WorkflowAuthorizationRequest(identity,
            new WorkflowAuthorizationScope(definition.Id.Value, definition.Version, nodeId, action)), cancellationToken)
            .ConfigureAwait(false);
        return decision.Allowed;
    }

    private static IdentityReference ToIdentity(ActorIdentity actor) =>
        new(actor.ProviderId.Value, actor.SubjectId);
    private static WorkflowStartResult Denied(string errorCode) => new(false, false, null, null, errorCode);
}

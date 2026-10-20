using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Extensions;

/// <summary>New definitions use declared business permissions; historical definitions keep exact grants.</summary>
public sealed class ModulePermissionAuthorizationService(
    IWorkflowPresentationCatalog catalog,
    IAuthorizationProvider permissions,
    IWorkflowAuthorizationService historical) : IWorkflowAuthorizationService
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken)
    {
        if (!catalog.TryGetProcess(request.Scope.WorkflowId, request.Scope.DefinitionVersion, out var process) || process is null)
            return new(false, "security.workflow-not-declared");
        var start = process.Definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start);
        var processPolicy = WorkflowStagePolicy.Read(start.Configuration);
        if (processPolicy is null) return await historical.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (request.Scope.NodeId is null)
        {
            var groups = request.Scope.Action.Value switch
            {
                "workflow.start" => processPolicy.Permissions,
                "instance.read" => processPolicy.ReadPermissions,
                "instance.cancel" => processPolicy.CancelPermissions,
                _ => [],
            };
            return await EvaluateAsync(request.Identity, groups, request.Scope, cancellationToken).ConfigureAwait(false);
        }
        var node = process.Definition.Nodes.SingleOrDefault(node => node.Id.Value == request.Scope.NodeId);
        if (node?.HumanTask is null) return new(false, "security.task-not-declared");
        var policy = WorkflowStagePolicy.Read(node.Configuration);
        if (policy is null) return new(false, "security.task-policy-missing");
        if (request.Scope.Action == WorkflowActions.AssignTask)
            return await permissions.AuthorizeAsync(new(request.Identity, WorkflowPermissions.ManageAccess), cancellationToken).ConfigureAwait(false);
        if (request.Scope.Action == WorkflowActions.ReadTask || request.Scope.Action == WorkflowActions.ClaimTask)
        {
            foreach (var action in policy.Actions.Values)
            {
                var decision = await EvaluateAsync(request.Identity, policy.Permissions.Concat(action.Permissions).ToArray(), request.Scope, cancellationToken).ConfigureAwait(false);
                if (decision.Allowed) return decision;
            }
            return new(false, "security.task-permission-missing");
        }
        if (!policy.Actions.TryGetValue(request.Scope.Action.Value, out var actionPolicy))
            return new(false, "security.action-not-declared");
        return await EvaluateAsync(request.Identity, policy.Permissions.Concat(actionPolicy.Permissions).ToArray(), request.Scope, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AuthorizationDecision> EvaluateAsync(IdentityReference identity, string[][] groups,
        WorkflowAuthorizationScope scope, CancellationToken cancellationToken)
    {
        if (groups.Length == 0) return new(false, "security.permission-requirement-missing");
        foreach (var group in groups)
        {
            var allowed = false;
            var unavailable = false;
            foreach (var permission in group)
            {
                var decision = await permissions.AuthorizeAsync(new(identity, new(permission), "workflow",
                    $"{scope.WorkflowId}/{scope.DefinitionVersion}/{scope.NodeId}/{scope.Action.Value}"), cancellationToken).ConfigureAwait(false);
                unavailable |= decision.ProviderUnavailable;
                if (decision.Allowed) { allowed = true; break; }
            }
            if (!allowed) return new(false, "security.module-permission-missing", ProviderUnavailable: unavailable);
        }
        return new(true, "security.module-permission");
    }
}

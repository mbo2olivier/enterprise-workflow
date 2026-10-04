namespace EnterpriseWorkflow.Security;

public sealed class WorkflowAccessAdministration(
    IAuthorizationProvider administrationAuthorization,
    IWorkflowAccessStore store,
    IWorkflowActionCatalog catalog)
{
    public async ValueTask<ProviderResult<WorkflowAccessGrant>> GrantAsync(
        IdentityReference actor,
        WorkflowAuthorizationScope scope,
        WorkflowGrantRecipient recipient,
        CancellationToken cancellationToken)
    {
        var authorized = await administrationAuthorization.AuthorizeAsync(
            new(actor, WorkflowPermissions.ManageAccess), cancellationToken).ConfigureAwait(false);
        if (!authorized.Allowed) return new(ProviderOutcome.Forbidden, ErrorCode: authorized.ReasonCode);
        if (!await catalog.ContainsAsync(scope, cancellationToken).ConfigureAwait(false))
            return new(ProviderOutcome.NotFound, ErrorCode: "security.workflow-action-not-declared");
        return await store.GrantAsync(new(scope, recipient), actor, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ProviderResult<bool>> RevokeAsync(
        IdentityReference actor,
        WorkflowAuthorizationScope scope,
        WorkflowGrantRecipient recipient,
        CancellationToken cancellationToken)
    {
        var authorized = await administrationAuthorization.AuthorizeAsync(
            new(actor, WorkflowPermissions.ManageAccess), cancellationToken).ConfigureAwait(false);
        if (!authorized.Allowed) return new(ProviderOutcome.Forbidden, ErrorCode: authorized.ReasonCode);
        return await store.RevokeAsync(scope, recipient, actor, cancellationToken).ConfigureAwait(false);
    }
}

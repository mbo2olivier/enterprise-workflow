namespace EnterpriseWorkflow.Security;

public sealed class InternalWorkflowAuthorizationService(
    IWorkflowAccessStore store,
    IReadOnlyDictionary<string, IIdentityDirectory> directories) : IWorkflowAuthorizationService
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlySet<string>? groups = null;
        if (directories.TryGetValue(request.Identity.ProviderId, out var directory))
        {
            var found = await directory.FindAsync(request.Identity, includeGroups: true, cancellationToken).ConfigureAwait(false);
            if (found.Outcome is ProviderOutcome.Unavailable)
            {
                var direct = await store.ResolveAsync(request.Identity, null, request.Scope, cancellationToken).ConfigureAwait(false);
                if (direct.Outcome is ProviderOutcome.Succeeded && direct.Value!.Allowed)
                    return new(true, "security.workflow-direct-grant", PolicyRevision: direct.Value.PolicyRevision);
                return new(false, "security.directory-unavailable", ProviderUnavailable: true, PolicyRevision: direct.Value?.PolicyRevision);
            }

            if (found.Outcome is ProviderOutcome.Succeeded) groups = found.Value!.GroupIds;
        }

        var resolved = await store.ResolveAsync(request.Identity, groups, request.Scope, cancellationToken).ConfigureAwait(false);
        if (resolved.Outcome is ProviderOutcome.Unavailable)
            return new(false, "security.workflow-store-unavailable", ProviderUnavailable: true);
        if (resolved.Outcome is not ProviderOutcome.Succeeded || resolved.Value is null)
            return new(false, "security.workflow-policy-unavailable", ProviderUnavailable: resolved.Outcome is not ProviderOutcome.NotFound);
        return resolved.Value.Allowed
            ? new(true, "security.workflow-grant", PolicyRevision: resolved.Value.PolicyRevision)
            : new(false, "security.workflow-grant-missing", PolicyRevision: resolved.Value.PolicyRevision);
    }
}

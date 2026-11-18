namespace EnterpriseWorkflow.Security;

public sealed class IdentityAdmissionAuthorizationProvider(IAuthorizationProvider inner,
    IEnumerable<IIdentityAdmissionProvider> providers) : IAuthorizationProvider
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        var denial = await IdentityAdmissionGuard.CheckAsync(request.Identity, providers, cancellationToken).ConfigureAwait(false);
        return denial ?? await inner.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class IdentityAdmissionWorkflowAuthorizationService(IWorkflowAuthorizationService inner,
    IEnumerable<IIdentityAdmissionProvider> providers) : IWorkflowAuthorizationService
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var denial = await IdentityAdmissionGuard.CheckAsync(request.Identity, providers, cancellationToken).ConfigureAwait(false);
        return denial ?? await inner.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

internal static class IdentityAdmissionGuard
{
    internal static async ValueTask<AuthorizationDecision?> CheckAsync(IdentityReference identity,
        IEnumerable<IIdentityAdmissionProvider> providers, CancellationToken cancellationToken)
    {
        var provider = providers.SingleOrDefault(candidate => candidate.ProviderId == identity.ProviderId);
        if (provider is null) return null;
        var result = await provider.GetAdmissionAsync(identity, cancellationToken).ConfigureAwait(false);
        return result.Outcome is ProviderOutcome.Succeeded && result.Value is IdentityAdmissionStatus.Active ? null
            : new(false, "security.identity-not-active", ProviderUnavailable: result.Outcome is ProviderOutcome.Unavailable);
    }
}

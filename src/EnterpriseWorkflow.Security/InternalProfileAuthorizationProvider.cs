namespace EnterpriseWorkflow.Security;

public sealed class InternalProfileAuthorizationProvider(
    ISecurityProfileStore store,
    IReadOnlyDictionary<string, IIdentityDirectory> directories) : IAuthorizationProvider
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlySet<string>? groups = null;
        if (directories.TryGetValue(request.Identity.ProviderId, out var directory))
        {
            var found = await directory.FindAsync(request.Identity, includeGroups: true, cancellationToken).ConfigureAwait(false);
            if (found.Outcome is ProviderOutcome.Unavailable)
            {
                var direct = await store.ResolvePermissionsAsync(request.Identity, null, cancellationToken).ConfigureAwait(false);
                if (direct.Outcome is ProviderOutcome.Succeeded && direct.Value!.Contains(request.Permission))
                    return new AuthorizationDecision(true, "security.direct-profile");
                return new AuthorizationDecision(false, "security.directory-unavailable", ProviderUnavailable: true);
            }

            if (found.Outcome is ProviderOutcome.Succeeded) groups = found.Value!.GroupIds;
        }

        var resolved = await store.ResolvePermissionsAsync(request.Identity, groups, cancellationToken).ConfigureAwait(false);
        if (resolved.Outcome is ProviderOutcome.Unavailable)
            return new AuthorizationDecision(false, "security.profile-store-unavailable", ProviderUnavailable: true);
        if (resolved.Outcome is not ProviderOutcome.Succeeded)
            return new AuthorizationDecision(false, "security.no-profile");
        return resolved.Value!.Contains(request.Permission)
            ? new AuthorizationDecision(true, "security.profile-permission")
            : new AuthorizationDecision(false, "security.permission-missing");
    }
}

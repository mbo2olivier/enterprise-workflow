namespace EnterpriseWorkflow.Security;

public sealed class AuthorizedIdentitySearch(IIdentityDirectory directory, IAuthorizationProvider authorization)
{
    public async ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(
        IdentityReference requester,
        string query,
        PermissionId candidatePermission,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        var canSearch = await authorization.AuthorizeAsync(new(requester, WorkflowPermissions.SearchApprovalCandidates), cancellationToken).ConfigureAwait(false);
        if (!canSearch.Allowed) return new(ProviderOutcome.Forbidden, ErrorCode: canSearch.ReasonCode);
        var candidates = await directory.SearchAsync(query, Math.Clamp(maximumResults * 3, 1, 100), cancellationToken).ConfigureAwait(false);
        if (candidates.Outcome is not ProviderOutcome.Succeeded || candidates.Value is null) return new(candidates.Outcome, ErrorCode: candidates.ErrorCode);
        var allowed = new List<DirectoryIdentity>(maximumResults);
        foreach (var candidate in candidates.Value)
        {
            var decision = await authorization.AuthorizeAsync(new(candidate.Identity, candidatePermission), cancellationToken).ConfigureAwait(false);
            if (decision.Allowed) allowed.Add(candidate);
            if (allowed.Count == maximumResults) break;
        }
        return new(ProviderOutcome.Succeeded, allowed);
    }
}

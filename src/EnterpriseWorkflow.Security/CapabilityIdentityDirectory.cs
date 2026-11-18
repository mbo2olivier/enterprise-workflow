namespace EnterpriseWorkflow.Security;

/// <summary>Compatibility adapter for hosts that implement only individual identity capabilities.</summary>
public sealed class CapabilityIdentityDirectory(string providerId, IIdentityResolver? resolver,
    IIdentitySearchProvider? search, IIdentityStatusProvider? status) : IIdentityDirectory
{
    public string ProviderId { get; } = providerId;
    public DirectoryCapabilities Capabilities => (search is null ? DirectoryCapabilities.None : DirectoryCapabilities.Search)
        | (status is null ? DirectoryCapabilities.None : DirectoryCapabilities.AccountStatus);

    public async ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(IdentityReference identity, bool includeGroups, CancellationToken cancellationToken)
    {
        if (identity.ProviderId != ProviderId) return new(ProviderOutcome.NotFound);
        if (resolver is not null) return await resolver.FindAsync(identity, includeGroups, cancellationToken).ConfigureAwait(false);
        if (status is null) return new(ProviderOutcome.NotFound);
        var result = await status.GetStatusAsync(identity, cancellationToken).ConfigureAwait(false);
        return result.Outcome is ProviderOutcome.Succeeded ? new(ProviderOutcome.Succeeded,
            new(identity, identity.SubjectId, null, new HashSet<string>(StringComparer.Ordinal), result.Value))
            : new(result.Outcome, ErrorCode: result.ErrorCode);
    }

    public ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(string query, int maximumResults, CancellationToken cancellationToken) =>
        search is null ? ValueTask.FromResult(new ProviderResult<IReadOnlyList<DirectoryIdentity>>(ProviderOutcome.NotFound))
            : search.SearchAsync(query, maximumResults, cancellationToken);
}

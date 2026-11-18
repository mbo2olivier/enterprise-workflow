namespace EnterpriseWorkflow.Security;

public interface IIdentityResolver
{
    string ProviderId { get; }
    ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(IdentityReference identity, bool includeGroups, CancellationToken cancellationToken);
}

public interface IIdentitySearchProvider
{
    string ProviderId { get; }
    ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(string query, int maximumResults, CancellationToken cancellationToken);
}

/// <summary>Optional remote status lookup. Absence leaves expiry and revocation local.</summary>
public interface IIdentityStatusProvider
{
    string ProviderId { get; }
    ValueTask<ProviderResult<DirectoryAccountStatus>> GetStatusAsync(IdentityReference identity, CancellationToken cancellationToken);
}

public enum IdentityAdmissionStatus { PendingApproval, Active, Rejected, Suspended }

/// <summary>Local admission gate, checked before session creation and on every validation.</summary>
public interface IIdentityAdmissionProvider
{
    string ProviderId { get; }
    ValueTask<ProviderResult<IdentityAdmissionStatus>> GetAdmissionAsync(IdentityReference identity, CancellationToken cancellationToken);
}

/// <summary>Resolves a stable identity from a third-party source; never creates a remote account.</summary>
public interface IIdentityImportProvider
{
    string ProviderId { get; }
    ValueTask<ProviderResult<DirectoryIdentity>> ResolveForImportAsync(string identifier, CancellationToken cancellationToken);
}

public sealed record IdentityProvisioningRequest(
    string Id, DirectoryIdentity Identity, IReadOnlyList<string> ProfileIds,
    IdentityReference Maker, IdentityAdmissionStatus Status, long Revision,
    IdentityReference? Checker = null, string? DecisionReason = null);

public sealed record IdentityProvisioningSubmission(string Identifier, IReadOnlyList<string> ProfileIds, IdentityReference Maker);
public sealed record IdentityProvisioningDecision(string RequestId, long ExpectedRevision, IdentityReference Checker, string? Reason = null);

/// <summary>
/// Optional local maker/checker capability. Implementations must atomically enforce unique identities,
/// distinct maker/checker, revision checks, source revalidation, profile activation and audit.
/// Pending identities must fail the inherited admission gate. No remote account is created.
/// </summary>
public interface IIdentityProvisioningProvider : IIdentityImportProvider, IIdentityAdmissionProvider
{
    new string ProviderId { get; }
    ValueTask<ProviderResult<IdentityProvisioningRequest>> SubmitAsync(IdentityProvisioningSubmission submission, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IdentityProvisioningRequest>> FindRequestAsync(string requestId, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IReadOnlyList<IdentityProvisioningRequest>>> ListPendingAsync(int maximumResults, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IdentityProvisioningRequest>> ApproveAsync(IdentityProvisioningDecision decision, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IdentityProvisioningRequest>> RejectAsync(IdentityProvisioningDecision decision, CancellationToken cancellationToken);
}

/// <summary>Capabilities derive from interfaces, never from configuration flags.</summary>
public sealed record IdentityProviderCapabilities(bool Resolve, bool Search, bool RemoteStatus, bool Import, bool Provisioning)
{
    public static IdentityProviderCapabilities From(object provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new(provider is IIdentityResolver, provider is IIdentityDirectory existingDirectory ? existingDirectory.Capabilities.HasFlag(DirectoryCapabilities.Search) : provider is IIdentitySearchProvider,
            provider is IIdentityStatusProvider || provider is IIdentityDirectory directory && directory.Capabilities.HasFlag(DirectoryCapabilities.AccountStatus),
            provider is IIdentityImportProvider, provider is IIdentityProvisioningProvider);
    }
}

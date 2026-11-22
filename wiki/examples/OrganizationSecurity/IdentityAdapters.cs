using EnterpriseWorkflow.Security;

namespace Example.Workflow.Security;

// Implement this boundary against your organization's actual identity service.
// Its implementation translates remote responses and handles bounded network calls.
public interface IOrganizationIdentityGateway
{
    ValueTask<AuthenticationResult> AuthenticateAsync(PasswordCredential credential, CancellationToken cancellationToken);
    ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(IdentityReference identity, bool includeGroups, CancellationToken cancellationToken);
    ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(string query, int maximumResults, CancellationToken cancellationToken);
    ValueTask<ProviderResult<DirectoryAccountStatus>> GetStatusAsync(IdentityReference identity, CancellationToken cancellationToken);
}

public sealed class OrganizationAuthentication(IOrganizationIdentityGateway gateway) : IAuthenticationProvider
{
    public const string Id = "example-corporate";
    public string ProviderId => Id;

    public async ValueTask<AuthenticationResult> AuthenticateAsync(PasswordCredential credential, CancellationToken cancellationToken)
    {
        var result = await gateway.AuthenticateAsync(credential, cancellationToken);
        // A gateway must return this provider's stable subject, never a mutable login.
        if (result.Status == AuthenticationStatus.Succeeded &&
            (result.Identity is not { } identity || identity.ProviderId != Id))
            return new(AuthenticationStatus.Unavailable, ErrorCode: "organization.invalid-identity");
        return result;
    }
}

public sealed class OrganizationIdentities(IOrganizationIdentityGateway gateway)
    : IIdentityResolver, IIdentitySearchProvider, IIdentityStatusProvider
{
    public string ProviderId => OrganizationAuthentication.Id;

    public ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(
        IdentityReference identity, bool includeGroups, CancellationToken cancellationToken) =>
        gateway.FindAsync(identity, includeGroups, cancellationToken);

    public ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(
        string query, int maximumResults, CancellationToken cancellationToken) =>
        gateway.SearchAsync(query, maximumResults, cancellationToken);

    public ValueTask<ProviderResult<DirectoryAccountStatus>> GetStatusAsync(
        IdentityReference identity, CancellationToken cancellationToken) =>
        gateway.GetStatusAsync(identity, cancellationToken);
}

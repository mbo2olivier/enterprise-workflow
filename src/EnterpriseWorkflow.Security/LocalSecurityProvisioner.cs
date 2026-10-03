namespace EnterpriseWorkflow.Security;

public sealed class LocalSecurityProvisioner(
    ILocalSecurityProvisioningStore store,
    LocalAuthenticationProvider localAuthentication)
{
    public async ValueTask<ProviderResult<bool>> BootstrapAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var normalized = Normalize(userName);
        var identity = new IdentityReference(SecurityProviderIds.Local, Guid.NewGuid().ToString("N"));
        var account = new LocalAccount(identity, userName.Trim(), normalized, string.Empty, true, 0, null, 0);
        account = account with { PasswordHash = localAuthentication.HashPassword(account, password) };
        var profile = new SecurityProfile("administrators", "Administrateurs", new HashSet<PermissionId>
        {
            WorkflowPermissions.ManageAccess, WorkflowPermissions.ReadAudit,
        }, GrantsAdministrativeAccess: true);
        return await store.BootstrapAsync(account, profile, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<ProviderResult<bool>> RecoverAsync(IdentityReference identity, CancellationToken cancellationToken) =>
        store.RecoverAdministrativeAccessAsync(identity, "administrators", cancellationToken);

    private static string Normalize(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        if (userName.Length > 256) throw new ArgumentException("User name is limited to 256 characters.", nameof(userName));
        return userName.Trim().ToUpperInvariant();
    }
}

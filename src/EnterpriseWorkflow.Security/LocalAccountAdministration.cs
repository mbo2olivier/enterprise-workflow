namespace EnterpriseWorkflow.Security;

public sealed class LocalAccountAdministration(
    ILocalAccountStore accounts,
    ILocalSecurityProvisioningStore provisioning,
    LocalAuthenticationProvider authentication)
{
    public async ValueTask<ProviderResult<LocalAccount>> CreateAsync(string userName, string password, IdentityReference actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        if (userName.Length > 256) throw new ArgumentException("User name is limited to 256 characters.", nameof(userName));
        var identity = new IdentityReference(SecurityProviderIds.Local, Guid.NewGuid().ToString("N"));
        var account = new LocalAccount(identity, userName.Trim(), userName.Trim().ToUpperInvariant(), string.Empty, true, 0, null, 0);
        account = account with { PasswordHash = authentication.HashPassword(account, password) };
        return await accounts.CreateAsync(account, actor, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ProviderResult<LocalAccount>> SetEnabledAsync(IdentityReference identity, bool enabled, IdentityReference actor, CancellationToken cancellationToken) =>
        await provisioning.ChangeLocalAccountAsync(identity, enabled, null, actor, cancellationToken).ConfigureAwait(false);

    public async ValueTask<ProviderResult<LocalAccount>> ResetPasswordAsync(IdentityReference identity, string password, IdentityReference actor, CancellationToken cancellationToken)
    {
        var found = await accounts.FindByIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
        if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null) return new(found.Outcome, ErrorCode: found.ErrorCode);
        var hash = authentication.HashPassword(found.Value, password);
        return await provisioning.ChangeLocalAccountAsync(identity, found.Value.Enabled, hash, actor, cancellationToken).ConfigureAwait(false);
    }
}

using Microsoft.AspNetCore.Identity;

namespace EnterpriseWorkflow.Security;

public sealed class LocalAuthenticationProvider(
    ILocalAccountStore accounts,
    SecurityPolicyOptions policy,
    TimeProvider timeProvider) : IAuthenticationProvider
{
    public const string LocalProviderId = SecurityProviderIds.Local;
    private readonly PasswordHasher<LocalAccount> _hasher = new();

    public string ProviderId => LocalProviderId;

    public async ValueTask<AuthenticationResult> AuthenticateAsync(PasswordCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        policy.Validate();
        if (string.IsNullOrWhiteSpace(credential.UserName) || string.IsNullOrEmpty(credential.Password) || credential.Password.Length > policy.PasswordMaximumLength)
            return new AuthenticationResult(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");

        var found = await accounts.FindByNormalizedNameAsync(credential.UserName.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (found.Outcome is ProviderOutcome.Unavailable)
            return new AuthenticationResult(AuthenticationStatus.Unavailable, ErrorCode: found.ErrorCode);
        if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null)
            return new AuthenticationResult(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");

        var account = found.Value;
        var now = timeProvider.GetUtcNow();
        if (!account.Enabled) return new AuthenticationResult(AuthenticationStatus.Rejected, ErrorCode: "security.account-disabled");
        if (account.LockoutEndUtc > now) return new AuthenticationResult(AuthenticationStatus.LockedOut, ErrorCode: "security.account-locked");

        var verification = _hasher.VerifyHashedPassword(account, account.PasswordHash, credential.Password);
        if (verification is PasswordVerificationResult.Failed)
        {
            var failed = await accounts.RecordAuthenticationFailureAsync(account.Identity, policy.MaximumFailedAccessAttempts, now + policy.LockoutDuration, cancellationToken).ConfigureAwait(false);
            var lockedOut = failed.Value?.LockoutEndUtc > now;
            return new AuthenticationResult(lockedOut ? AuthenticationStatus.LockedOut : AuthenticationStatus.Rejected,
                ErrorCode: "security.invalid-credentials");
        }

        var passwordHash = verification is PasswordVerificationResult.SuccessRehashNeeded
            ? _hasher.HashPassword(account, credential.Password)
            : account.PasswordHash;
        var updated = await accounts.RecordAuthenticationSuccessAsync(account.Identity,
            passwordHash == account.PasswordHash ? null : passwordHash, cancellationToken).ConfigureAwait(false);
        return updated.Outcome is ProviderOutcome.Succeeded
            ? new AuthenticationResult(AuthenticationStatus.Succeeded, account.Identity)
            : new AuthenticationResult(AuthenticationStatus.Unavailable, ErrorCode: updated.ErrorCode);
    }

    public string HashPassword(LocalAccount account, string password)
    {
        policy.ValidateNewPassword(password);
        return _hasher.HashPassword(account, password);
    }
}

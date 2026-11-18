using System.Security.Cryptography;
using System.Text;

namespace EnterpriseWorkflow.Security;

public sealed class SecuritySessionManager

{
    private readonly ISecuritySessionStore store;
    private readonly SecurityPolicyOptions policy;
    private readonly IReadOnlyDictionary<string, IIdentityDirectory> directories;
    private readonly TimeProvider timeProvider;
    private readonly IEnumerable<IIdentityStatusProvider> statusProviders;
    private readonly IEnumerable<IIdentityAdmissionProvider> admissionProviders;
    private readonly SecondFactorOptions secondFactorOptions;

    public SecuritySessionManager(ISecuritySessionStore store, SecurityPolicyOptions policy,
        IReadOnlyDictionary<string, IIdentityDirectory> directories, TimeProvider timeProvider)
        : this(store, policy, directories, timeProvider, [], []) { }

    public SecuritySessionManager(ISecuritySessionStore store, SecurityPolicyOptions policy,
        IReadOnlyDictionary<string, IIdentityDirectory> directories, TimeProvider timeProvider,
        IEnumerable<IIdentityStatusProvider> statusProviders, IEnumerable<IIdentityAdmissionProvider> admissionProviders)
        : this(store, policy, directories, timeProvider, statusProviders, admissionProviders, new()) { }

    public SecuritySessionManager(ISecuritySessionStore store, SecurityPolicyOptions policy,
        IReadOnlyDictionary<string, IIdentityDirectory> directories, TimeProvider timeProvider,
        IEnumerable<IIdentityStatusProvider> statusProviders, IEnumerable<IIdentityAdmissionProvider> admissionProviders, SecondFactorOptions secondFactorOptions)
    {
        this.store = store; this.policy = policy; this.directories = directories; this.timeProvider = timeProvider;
        this.secondFactorOptions = secondFactorOptions;
        this.statusProviders = statusProviders.ToArray(); this.admissionProviders = admissionProviders.ToArray();
    }

    public async ValueTask<bool> CanIssueAsync(IdentityReference identity, CancellationToken cancellationToken) =>
        await CheckAdmissionAsync(identity, cancellationToken).ConfigureAwait(false) is null;

    public ValueTask<ProviderResult<SessionIssueResult>> IssueAsync(IdentityReference identity, CancellationToken cancellationToken) =>
        IssueAsync(identity, false, cancellationToken);

    /// <summary>Only the trusted host may assert successful consumption of a second-factor proof.</summary>
    public async ValueTask<ProviderResult<SessionIssueResult>> IssueAsync(IdentityReference identity, bool secondFactorVerified, CancellationToken cancellationToken)
    {
        if (secondFactorOptions.Enabled && !secondFactorVerified) return new(ProviderOutcome.Forbidden, ErrorCode: "security.second-factor-required");
        policy.Validate();
        var admission = await CheckAdmissionAsync(identity, cancellationToken).ConfigureAwait(false);
        if (admission is not null) return new(admission.Value, ErrorCode: "security.identity-not-active");
        var now = Millisecond(timeProvider.GetUtcNow());
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var digest = Digest(token);
        var session = new SecuritySession(digest, identity, now, now, now + policy.AbsoluteSessionLifetime, now, false, 0) { SecondFactorVerified = secondFactorVerified };
        var created = await store.CreateSessionAsync(session, cancellationToken).ConfigureAwait(false);
        return created.Outcome is ProviderOutcome.Succeeded
            ? new(ProviderOutcome.Succeeded, new SessionIssueResult(token, session.ExpiresAtUtc))
            : new(created.Outcome, ErrorCode: created.ErrorCode);
    }

    public ValueTask<SessionValidationResult> ValidateAsync(string token, CancellationToken cancellationToken) =>
        ValidateAsync(token, 3, cancellationToken);

    private async ValueTask<SessionValidationResult> ValidateAsync(string token, int retries, CancellationToken cancellationToken)
    {
        policy.Validate();
        if (string.IsNullOrWhiteSpace(token)) return new(SessionValidationStatus.NotFound, ErrorCode: "security.session-not-found");
        var found = await store.FindSessionAsync(Digest(token), cancellationToken).ConfigureAwait(false);
        if (found.Outcome is ProviderOutcome.Unavailable) return new(SessionValidationStatus.Unavailable, ErrorCode: found.ErrorCode);
        if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null) return new(SessionValidationStatus.NotFound, ErrorCode: "security.session-not-found");
        var session = found.Value; var now = Millisecond(timeProvider.GetUtcNow());
        if (secondFactorOptions.Enabled && !session.SecondFactorVerified)
            return new(SessionValidationStatus.RevalidationRequired, ErrorCode: "security.second-factor-required");
        if (session.Revoked) return new(SessionValidationStatus.Revoked, ErrorCode: "security.session-revoked");
        if (session.ExpiresAtUtc <= now || session.LastSeenAtUtc + policy.IdleSessionTimeout <= now) return new(SessionValidationStatus.Expired, ErrorCode: "security.session-expired");

        var admission = await CheckAdmissionAsync(session.Identity, cancellationToken).ConfigureAwait(false);
        if (admission is not null)
        {
            if (admission is ProviderOutcome.Unavailable) return new(SessionValidationStatus.Unavailable, ErrorCode: "security.identity-status-unavailable");
            await store.RevokeSessionAsync(session.TokenDigest, session.Identity, cancellationToken).ConfigureAwait(false);
            return new(SessionValidationStatus.Revoked, ErrorCode: "security.identity-not-active");
        }
        var checkedAt = session.RemoteStatusCheckedAtUtc;
        if (session.Identity.ProviderId != SecurityProviderIds.Local && checkedAt + policy.RemoteStatusRevalidationInterval <= now)
        {
            var statusProvider = statusProviders?.SingleOrDefault(provider => provider.ProviderId == session.Identity.ProviderId);
            directories.TryGetValue(session.Identity.ProviderId, out var directory);
            if (statusProvider is not null)
            {
                var status = await statusProvider.GetStatusAsync(session.Identity, cancellationToken).ConfigureAwait(false);
                if (status.Outcome is ProviderOutcome.Unavailable || status.Outcome is ProviderOutcome.Succeeded && status.Value is DirectoryAccountStatus.Unknown)
                    return new(SessionValidationStatus.Unavailable, ErrorCode: status.ErrorCode ?? "security.remote-status-unknown");
                if (status.Outcome is not ProviderOutcome.Succeeded || status.Value is not DirectoryAccountStatus.Enabled)
                {
                    await store.RevokeSessionAsync(session.TokenDigest, session.Identity, cancellationToken).ConfigureAwait(false);
                    return new(SessionValidationStatus.Revoked, ErrorCode: "security.remote-identity-revoked");
                }
                checkedAt = now;
            }
            else if (directory is not null && directory.Capabilities.HasFlag(DirectoryCapabilities.AccountStatus))
            {
                var identity = await directory.FindAsync(session.Identity, includeGroups: false, cancellationToken).ConfigureAwait(false);
                if (identity.Outcome is ProviderOutcome.Unavailable) return new(SessionValidationStatus.Unavailable, ErrorCode: identity.ErrorCode);
                if (identity.Outcome is not ProviderOutcome.Succeeded || identity.Value?.AccountStatus is DirectoryAccountStatus.Disabled)
                {
                    await store.RevokeSessionAsync(session.TokenDigest, session.Identity, cancellationToken).ConfigureAwait(false);
                    return new(SessionValidationStatus.Revoked, ErrorCode: "security.remote-identity-revoked");
                }
                if (identity.Value?.AccountStatus is not DirectoryAccountStatus.Enabled)
                    return new(SessionValidationStatus.Unavailable, ErrorCode: "security.remote-status-unknown");
                checkedAt = now;
            }
        }

        var updated = await store.UpdateSessionAsync(session with { LastSeenAtUtc = now, RemoteStatusCheckedAtUtc = checkedAt }, cancellationToken).ConfigureAwait(false);
        // Parallel browser requests can touch the same session revision. Read it
        // again so revocation/expiry are checked before retrying the heartbeat.
        if (updated.Outcome is ProviderOutcome.Conflict && retries > 0)
            return await ValidateAsync(token, retries - 1, cancellationToken).ConfigureAwait(false);
        return updated.Outcome is ProviderOutcome.Succeeded
            ? new(SessionValidationStatus.Valid, session.Identity)
            : new(SessionValidationStatus.Unavailable, ErrorCode: updated.ErrorCode);
    }

    public ValueTask<ProviderResult<bool>> RevokeAsync(string token, IdentityReference actor, CancellationToken cancellationToken) =>
        store.RevokeSessionAsync(Digest(token), actor, cancellationToken);

    private async ValueTask<ProviderOutcome?> CheckAdmissionAsync(IdentityReference identity, CancellationToken cancellationToken)
    {
        var provider = admissionProviders?.SingleOrDefault(candidate => candidate.ProviderId == identity.ProviderId);
        if (provider is null) return null;
        var result = await provider.GetAdmissionAsync(identity, cancellationToken).ConfigureAwait(false);
        return result.Outcome is ProviderOutcome.Succeeded && result.Value is IdentityAdmissionStatus.Active
            ? null : result.Outcome is ProviderOutcome.Unavailable ? ProviderOutcome.Unavailable : ProviderOutcome.Forbidden;
    }

    private static string Digest(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static DateTimeOffset Millisecond(DateTimeOffset value) => DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());
}

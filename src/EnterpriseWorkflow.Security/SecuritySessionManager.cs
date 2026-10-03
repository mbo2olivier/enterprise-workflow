using System.Security.Cryptography;
using System.Text;

namespace EnterpriseWorkflow.Security;

public sealed class SecuritySessionManager(
    ISecuritySessionStore store,
    SecurityPolicyOptions policy,
    IReadOnlyDictionary<string, IIdentityDirectory> directories,
    TimeProvider timeProvider)
{
    public async ValueTask<ProviderResult<SessionIssueResult>> IssueAsync(IdentityReference identity, CancellationToken cancellationToken)
    {
        policy.Validate();
        var now = Millisecond(timeProvider.GetUtcNow());
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var digest = Digest(token);
        var session = new SecuritySession(digest, identity, now, now, now + policy.AbsoluteSessionLifetime, now, false, 0);
        var created = await store.CreateSessionAsync(session, cancellationToken).ConfigureAwait(false);
        return created.Outcome is ProviderOutcome.Succeeded
            ? new(ProviderOutcome.Succeeded, new SessionIssueResult(token, session.ExpiresAtUtc))
            : new(created.Outcome, ErrorCode: created.ErrorCode);
    }

    public async ValueTask<SessionValidationResult> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        policy.Validate();
        if (string.IsNullOrWhiteSpace(token)) return new(SessionValidationStatus.NotFound, ErrorCode: "security.session-not-found");
        var found = await store.FindSessionAsync(Digest(token), cancellationToken).ConfigureAwait(false);
        if (found.Outcome is ProviderOutcome.Unavailable) return new(SessionValidationStatus.Unavailable, ErrorCode: found.ErrorCode);
        if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null) return new(SessionValidationStatus.NotFound, ErrorCode: "security.session-not-found");
        var session = found.Value; var now = Millisecond(timeProvider.GetUtcNow());
        if (session.Revoked) return new(SessionValidationStatus.Revoked, ErrorCode: "security.session-revoked");
        if (session.ExpiresAtUtc <= now || session.LastSeenAtUtc + policy.IdleSessionTimeout <= now) return new(SessionValidationStatus.Expired, ErrorCode: "security.session-expired");

        var checkedAt = session.RemoteStatusCheckedAtUtc;
        if (session.Identity.ProviderId != SecurityProviderIds.Local && checkedAt + policy.RemoteStatusRevalidationInterval <= now)
        {
            if (!directories.TryGetValue(session.Identity.ProviderId, out var directory)) return new(SessionValidationStatus.Unavailable, ErrorCode: "security.directory-unavailable");
            var identity = await directory.FindAsync(session.Identity, includeGroups: false, cancellationToken).ConfigureAwait(false);
            if (identity.Outcome is ProviderOutcome.Unavailable) return new(SessionValidationStatus.Unavailable, ErrorCode: identity.ErrorCode);
            if (identity.Outcome is not ProviderOutcome.Succeeded) { await store.RevokeSessionAsync(session.TokenDigest, session.Identity, cancellationToken).ConfigureAwait(false); return new(SessionValidationStatus.Revoked, ErrorCode: "security.remote-identity-revoked"); }
            checkedAt = now;
        }

        var updated = await store.UpdateSessionAsync(session with { LastSeenAtUtc = now, RemoteStatusCheckedAtUtc = checkedAt }, cancellationToken).ConfigureAwait(false);
        return updated.Outcome is ProviderOutcome.Succeeded
            ? new(SessionValidationStatus.Valid, session.Identity)
            : new(SessionValidationStatus.Unavailable, ErrorCode: updated.ErrorCode);
    }

    public ValueTask<ProviderResult<bool>> RevokeAsync(string token, IdentityReference actor, CancellationToken cancellationToken) =>
        store.RevokeSessionAsync(Digest(token), actor, cancellationToken);

    private static string Digest(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static DateTimeOffset Millisecond(DateTimeOffset value) => DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());
}

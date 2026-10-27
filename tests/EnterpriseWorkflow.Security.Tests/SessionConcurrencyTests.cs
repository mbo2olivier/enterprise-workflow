using Xunit;

namespace EnterpriseWorkflow.Security.Tests;

public sealed class SessionConcurrencyTests
{
    [Theory]
    [InlineData(false, false, SessionValidationStatus.Valid, 2)]
    [InlineData(true, false, SessionValidationStatus.Revoked, 1)]
    [InlineData(false, true, SessionValidationStatus.Unavailable, 4)]
    public async Task ConflictsRecheckSessionAndHaveABoundedRetry(bool revoke, bool alwaysConflict, SessionValidationStatus expected, int updates)
    {
        var store = new ConflictingStore(revoke, alwaysConflict);
        var manager = new SecuritySessionManager(store, new(), new Dictionary<string, IIdentityDirectory>(), TimeProvider.System);
        var result = await manager.ValidateAsync("test-token", TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Status);
        Assert.Equal(updates, store.Updates);
    }

    private sealed class ConflictingStore(bool revoke, bool alwaysConflict) : ISecuritySessionStore
    {
        private SecuritySession _session = new("digest", new("local", "alice"), DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow, false, 0);
        public int Updates { get; private set; }
        public ValueTask<ProviderResult<SecuritySession>> FindSessionAsync(string tokenDigest, CancellationToken cancellationToken) => ValueTask.FromResult(new ProviderResult<SecuritySession>(ProviderOutcome.Succeeded, _session));
        public ValueTask<ProviderResult<SecuritySession>> UpdateSessionAsync(SecuritySession session, CancellationToken cancellationToken)
        {
            Updates++;
            if (Updates == 1 || alwaysConflict)
            {
                _session = _session with { Revision = _session.Revision + 1, Revoked = revoke };
                return ValueTask.FromResult(new ProviderResult<SecuritySession>(ProviderOutcome.Conflict, ErrorCode: "security.stale-session"));
            }
            _session = session;
            return ValueTask.FromResult(new ProviderResult<SecuritySession>(ProviderOutcome.Succeeded, session));
        }
        public ValueTask<ProviderResult<SecuritySession>> CreateSessionAsync(SecuritySession session, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<bool>> RevokeSessionAsync(string tokenDigest, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ProviderResult<int>> RevokeIdentitySessionsAsync(IdentityReference identity, IdentityReference actor, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

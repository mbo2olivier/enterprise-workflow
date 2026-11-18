namespace EnterpriseWorkflow.Security;

public sealed class SecondFactorOptions
{
    public bool Enabled { get; set; }
}

/// <summary>The host generates AttemptId; neither context contains the primary password.</summary>
public sealed record SecondFactorContext(IdentityReference Identity, string AttemptId);
public sealed record SecondFactorChallenge(string ChallengeId, DateTimeOffset ExpiresAtUtc, string? Prompt = null);
public sealed record SecondFactorVerificationContext(IdentityReference Identity, string AttemptId, string ChallengeId, string Proof);
public enum SecondFactorStatus { Succeeded, Rejected, Pending, Expired, Unavailable }
public sealed record SecondFactorVerification(SecondFactorStatus Status, string? ErrorCode = null);

/// <summary>
/// Compiled host extension. The implementation owns challenge storage, bounded attempts, expiry and
/// atomic single consumption. A successful verification must never be returned twice for a challenge.
/// Bind challenges to both Identity and AttemptId. Proofs and primary credentials must not be logged.
/// </summary>
public interface ISecondFactorHandler
{
    ValueTask<ProviderResult<SecondFactorChallenge>> BeginAsync(SecondFactorContext context, CancellationToken cancellationToken);
    ValueTask<SecondFactorVerification> VerifyAsync(SecondFactorVerificationContext context, CancellationToken cancellationToken);
    ValueTask CancelAsync(SecondFactorContext context, string challengeId, CancellationToken cancellationToken);
}

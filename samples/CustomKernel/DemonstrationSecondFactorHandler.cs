using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Samples.CustomKernel;

/// <summary>
/// Demonstrates the contract without issuing or accepting a usable proof.
/// Replace with the organization's real bounded, single-consumption challenge service.
/// </summary>
public sealed class DemonstrationSecondFactorHandler : ISecondFactorHandler
{
    public ValueTask<ProviderResult<SecondFactorChallenge>> BeginAsync(SecondFactorContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ProviderResult<SecondFactorChallenge>(ProviderOutcome.Unavailable, ErrorCode: "sample.second-factor-not-configured"));
    public ValueTask<SecondFactorVerification> VerifyAsync(SecondFactorVerificationContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new SecondFactorVerification(SecondFactorStatus.Unavailable));
    public ValueTask CancelAsync(SecondFactorContext context, string challengeId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

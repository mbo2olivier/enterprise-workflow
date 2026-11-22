using EnterpriseWorkflow.Security;

namespace Example.Workflow.Security;

// Implement with a durable/shared challenge service. The gateway owns identity +
// attempt binding, expiry, bounded attempts and atomic single-use verification.
public interface IOrganizationSecondFactorGateway
{
    ValueTask<ProviderResult<SecondFactorChallenge>> BeginAsync(SecondFactorContext context, CancellationToken cancellationToken);
    ValueTask<SecondFactorVerification> VerifyAsync(SecondFactorVerificationContext context, CancellationToken cancellationToken);
    ValueTask CancelAsync(SecondFactorContext context, string challengeId, CancellationToken cancellationToken);
}

public sealed class OrganizationSecondFactor(IOrganizationSecondFactorGateway gateway) : ISecondFactorHandler
{
    public ValueTask<ProviderResult<SecondFactorChallenge>> BeginAsync(
        SecondFactorContext context, CancellationToken cancellationToken) =>
        gateway.BeginAsync(context, cancellationToken);

    public ValueTask<SecondFactorVerification> VerifyAsync(
        SecondFactorVerificationContext context, CancellationToken cancellationToken) =>
        gateway.VerifyAsync(context, cancellationToken);

    public ValueTask CancelAsync(
        SecondFactorContext context, string challengeId, CancellationToken cancellationToken) =>
        gateway.CancelAsync(context, challengeId, cancellationToken);
}

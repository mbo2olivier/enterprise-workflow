using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

public enum ExternalEffectDeliveryDisposition
{
    Delivered,
    RetryableFailure,
    PermanentFailure,
}

public sealed record ExternalEffectDeliveryResult(
    ExternalEffectDeliveryDisposition Disposition,
    string? ErrorCode = null)
{
    public static ExternalEffectDeliveryResult Success() => new(ExternalEffectDeliveryDisposition.Delivered);
    public static ExternalEffectDeliveryResult Retryable(string errorCode) =>
        new(ExternalEffectDeliveryDisposition.RetryableFailure, errorCode);
    public static ExternalEffectDeliveryResult PermanentFailure(string errorCode) =>
        new(ExternalEffectDeliveryDisposition.PermanentFailure, errorCode);
}

/// <summary>Delivers one durable external-effect intent using its stable idempotency key.</summary>
public interface IExternalEffectTransport
{
    ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken);
}

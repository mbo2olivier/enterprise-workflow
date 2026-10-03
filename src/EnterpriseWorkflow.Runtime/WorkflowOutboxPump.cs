using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Persistence;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Claims and delivers at most one due external-effect intent.</summary>
public interface IWorkflowOutboxPump
{
    ValueTask<bool> DeliverNextAsync(TechnicalId ownerId, CancellationToken cancellationToken);
}

internal sealed class WorkflowOutboxPump(
    IWorkflowStore store,
    IExternalEffectTransport transport,
    IOptions<WorkflowRuntimeOptions> configuredOptions,
    IWorkflowRetryDelayStrategy retry,
    TimeProvider timeProvider) : IWorkflowOutboxPump
{
    private readonly WorkflowRuntimeOptions _options = Validate(configuredOptions.Value);

    public async ValueTask<bool> DeliverNextAsync(TechnicalId ownerId, CancellationToken cancellationToken)
    {
        var claim = await store.ClaimDueOutboxAsync(
            new ClaimDueOutboxCommand(ownerId, _options.OutboxLeaseDuration), cancellationToken).ConfigureAwait(false);
        if (claim.Outcome is StoreOutcome.NotFound) return false;
        if (claim.Outcome is not StoreOutcome.Succeeded || claim.Value is null)
            throw new WorkflowRuntimeException(claim.ErrorCode ?? "EW4092_OUTBOX_CLAIM_FAILED", "The store could not claim an outbox message.");

        ExternalEffectDeliveryResult delivery;
        try
        {
            delivery = await transport.DeliverAsync(claim.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            delivery = ExternalEffectDeliveryResult.Retryable("runtime.outbox-transport-exception");
        }

        CommitOutboxCommand commit;
        if (delivery.Disposition is ExternalEffectDeliveryDisposition.Delivered)
        {
            commit = new CommitOutboxCommand(
                claim.Value.MessageId, claim.Value.Token, claim.Value.Generation,
                OutboxCommitKind.Delivered, null, null);
        }
        else if (delivery.Disposition is ExternalEffectDeliveryDisposition.PermanentFailure ||
                 claim.Value.Attempt >= _options.OutboxMaximumAttempts)
        {
            commit = new CommitOutboxCommand(
                claim.Value.MessageId, claim.Value.Token, claim.Value.Generation,
                OutboxCommitKind.Fail, null, delivery.ErrorCode ?? "runtime.outbox-retries-exhausted");
        }
        else
        {
            var delay = retry.GetDelay(
                claim.Value.Attempt, _options.OutboxRetryBaseDelay, _options.OutboxRetryMaximumDelay);
            var retryAt = DateTimeOffset.FromUnixTimeMilliseconds(
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds()) + delay;
            commit = new CommitOutboxCommand(
                claim.Value.MessageId, claim.Value.Token, claim.Value.Generation,
                OutboxCommitKind.Retry, retryAt, delivery.ErrorCode);
        }

        var result = await store.CommitOutboxAsync(commit, cancellationToken).ConfigureAwait(false);
        if (result.Outcome is not StoreOutcome.Succeeded)
            throw new WorkflowRuntimeException(result.ErrorCode ?? "EW4093_OUTBOX_COMMIT_FAILED", "The outbox delivery result was not committed.");
        return true;
    }

    private static WorkflowRuntimeOptions Validate(WorkflowRuntimeOptions options)
    {
        options.Validate();
        return options;
    }
}

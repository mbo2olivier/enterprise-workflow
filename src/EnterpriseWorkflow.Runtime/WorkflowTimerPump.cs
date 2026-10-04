using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Resumes due durable timers after normal polling or process restart.</summary>
public interface IWorkflowTimerPump
{
    ValueTask<bool> FireNextAsync(CancellationToken cancellationToken);
}

internal sealed class WorkflowTimerPump(IWorkflowStore store) : IWorkflowTimerPump
{
    public async ValueTask<bool> FireNextAsync(CancellationToken cancellationToken)
    {
        var result = await store.FireNextDueTimerAsync(cancellationToken).ConfigureAwait(false);
        if (result.Outcome is StoreOutcome.NotFound) return false;
        if (result.Outcome is StoreOutcome.Succeeded) return true;
        throw new WorkflowRuntimeException(result.ErrorCode ?? "EW4092_TIMER_FIRE_FAILED", "The due timer was not fired.");
    }
}

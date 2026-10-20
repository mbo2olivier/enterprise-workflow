using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Default completion with no implicit state replacement or external effects.</summary>
public sealed class StandardHumanTaskHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var policy = WorkflowStagePolicy.Read(context.Configuration);
        if (policy is null || !policy.Actions.ContainsKey(context.ActionId.Value))
            return ValueTask.FromResult(HumanTaskCompletionResult.Rejected(new("runtime.action-not-declared")));
        return ValueTask.FromResult(HumanTaskCompletionResult.Success(context.ActionId));
    }
}

using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Claims and executes at most one due workflow node.</summary>
public interface IWorkflowExecutionPump
{
    ValueTask<bool> ExecuteNextAsync(TechnicalId ownerId, CancellationToken cancellationToken);
}

internal sealed class WorkflowExecutionPump : IWorkflowExecutionPump
{
    private static readonly TechnicalId UnhandledError = new("runtime.unhandled-handler-exception");
    private static readonly TechnicalId TimeoutError = new("runtime.handler-timeout");
    private readonly IWorkflowStore _store;
    private readonly IScopedWorkflowHandlerResolver _resolver;
    private readonly WorkflowRuntimeOptions _options;
    private readonly IWorkflowRetryDelayStrategy _retry;
    private readonly TimeProvider _timeProvider;

    public WorkflowExecutionPump(
        IWorkflowStore store,
        IScopedWorkflowHandlerResolver resolver,
        IOptions<WorkflowRuntimeOptions> options,
        IWorkflowRetryDelayStrategy retry,
        TimeProvider timeProvider)
    {
        _store = store;
        _resolver = resolver;
        _options = options.Value;
        _options.Validate();
        _retry = retry;
        _timeProvider = timeProvider;
    }

    public async ValueTask<bool> ExecuteNextAsync(TechnicalId ownerId, CancellationToken cancellationToken)
    {
        var claim = await _store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(ownerId, _options.LeaseDuration), cancellationToken).ConfigureAwait(false);
        if (claim.Outcome is StoreOutcome.NotFound)
            return false;
        if (claim.Outcome is not StoreOutcome.Succeeded || claim.Value is null)
            throw new WorkflowRuntimeException(claim.ErrorCode ?? "EW4090_CLAIM_FAILED", "The workflow store could not claim due work.");

        await ExecuteClaimedAsync(claim.Value, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task ExecuteClaimedAsync(ClaimedWork claimed, CancellationToken stoppingToken)
    {
        var node = claimed.Definition.Nodes.SingleOrDefault(item => item.Id == claimed.Lease.NodeId)
            ?? throw new WorkflowRuntimeException("EW4006_NODE_NOT_FOUND", "The claimed node is absent from its immutable definition.");
        if (node.Role is WorkflowNodeRole.Start)
        {
            await CommitSuccessAsync(claimed, node, null, NodeStateUpdate.Unchanged, [], stoppingToken).ConfigureAwait(false);
            return;
        }
        if (node.Role is WorkflowNodeRole.End)
        {
            await CommitAsync(claimed, NodeCommitKind.Complete, null, null, null, [], stoppingToken).ConfigureAwait(false);
            return;
        }

        using var timeout = new CancellationTokenSource(_options.AttemptTimeout, _timeProvider);
        using var handlerCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
        using var heartbeatStop = new CancellationTokenSource();
        var leaseLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeat = RenewLeaseAsync(claimed.Lease, handlerCancellation, heartbeatStop, leaseLost, stoppingToken);
        try
        {
            if (node.Role is WorkflowNodeRole.Service)
            {
                var result = await InvokeServiceAsync(claimed, node, handlerCancellation.Token).ConfigureAwait(false);
                heartbeatStop.Cancel();
                await IgnoreHeartbeatCancellationAsync(heartbeat).ConfigureAwait(false);
                if (leaseLost.Task.IsCompleted || stoppingToken.IsCancellationRequested) return;
                if (timeout.IsCancellationRequested)
                    await CommitFailureAsync(claimed, TimeoutError, stoppingToken).ConfigureAwait(false);
                else
                    await CommitServiceResultAsync(claimed, node, result, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                var result = await InvokeDecisionAsync(claimed, node, handlerCancellation.Token).ConfigureAwait(false);
                heartbeatStop.Cancel();
                await IgnoreHeartbeatCancellationAsync(heartbeat).ConfigureAwait(false);
                if (leaseLost.Task.IsCompleted || stoppingToken.IsCancellationRequested) return;
                if (timeout.IsCancellationRequested)
                    await CommitFailureAsync(claimed, TimeoutError, stoppingToken).ConfigureAwait(false);
                else
                    await CommitDecisionResultAsync(claimed, node, result, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || leaseLost.Task.IsCompleted)
        {
            heartbeatStop.Cancel();
            await IgnoreHeartbeatCancellationAsync(heartbeat).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            heartbeatStop.Cancel();
            await IgnoreHeartbeatCancellationAsync(heartbeat).ConfigureAwait(false);
            if (!leaseLost.Task.IsCompleted && !stoppingToken.IsCancellationRequested)
                await CommitFailureAsync(claimed, TimeoutError, stoppingToken).ConfigureAwait(false);
        }
        catch (WorkflowRuntimeException)
        {
            throw;
        }
        catch
        {
            heartbeatStop.Cancel();
            await IgnoreHeartbeatCancellationAsync(heartbeat).ConfigureAwait(false);
            if (!leaseLost.Task.IsCompleted && !stoppingToken.IsCancellationRequested)
                await CommitAsync(claimed, NodeCommitKind.Fail, null, null, UnhandledError.Value, [], stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<ServiceNodeResult> InvokeServiceAsync(ClaimedWork claimed, WorkflowNode node, CancellationToken token)
    {
        await using var scope = _resolver.ResolveService(node.Handler!);
        return await scope.Handler.ExecuteAsync(CreateContext(claimed, node), token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A service handler returned null.");
    }

    private async Task<DecisionNodeResult> InvokeDecisionAsync(ClaimedWork claimed, WorkflowNode node, CancellationToken token)
    {
        await using var scope = _resolver.ResolveDecision(node.Handler!);
        return await scope.Handler.EvaluateAsync(CreateContext(claimed, node), token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A decision handler returned null.");
    }

    private static NodeExecutionContext CreateContext(ClaimedWork claimed, WorkflowNode node) => new(
        claimed.Lease.InstanceId, claimed.Lease.ActivationId, node.Id, claimed.Attempt, claimed.State, node.Configuration);

    private async Task CommitServiceResultAsync(
        ClaimedWork claimed, WorkflowNode node, ServiceNodeResult result, CancellationToken token)
    {
        if (result.Disposition is NodeExecutionDisposition.Succeeded)
        {
            var outbox = result.Effects.Select(item => new OutboxWrite(
                item.OperationId, item.Destination, item.ContentType, item.Payload.CanonicalText)).ToArray();
            await CommitSuccessAsync(claimed, node, null, result.StateUpdate, outbox, token).ConfigureAwait(false);
        }
        else
        {
            await CommitFailureAsync(claimed, result.ErrorCode!.Value, token, result.Disposition).ConfigureAwait(false);
        }
    }

    private async Task CommitDecisionResultAsync(
        ClaimedWork claimed, WorkflowNode node, DecisionNodeResult result, CancellationToken token)
    {
        if (result.Disposition is NodeExecutionDisposition.Succeeded)
            await CommitSuccessAsync(claimed, node, result.Outcome, result.StateUpdate, [], token).ConfigureAwait(false);
        else
            await CommitFailureAsync(claimed, result.ErrorCode!.Value, token, result.Disposition).ConfigureAwait(false);
    }

    private async Task CommitSuccessAsync(
        ClaimedWork claimed,
        WorkflowNode node,
        TechnicalId? outcome,
        NodeStateUpdate stateUpdate,
        IReadOnlyList<OutboxWrite> outbox,
        CancellationToken token)
    {
        var transition = node.Role is WorkflowNodeRole.Decision
            ? claimed.Definition.Transitions.SingleOrDefault(item =>
                item.SourceId == node.Id && ((item.Outcome == outcome) || (item.IsDefault && outcome is not null &&
                    !claimed.Definition.Transitions.Any(candidate => candidate.SourceId == node.Id && candidate.Outcome == outcome))))
            : claimed.Definition.Transitions.Single(item => item.SourceId == node.Id);
        if (transition is null)
            throw new InvalidOperationException("The decision handler selected no declared transition.");

        WorkflowState? replacement = null;
        if (stateUpdate.ReplacesState)
        {
            var element = stateUpdate.GetReplacement()!.Value;
            replacement = WorkflowState.Create(element.GetRawText(), claimed.State.SchemaVersion);
        }

        await CommitAsync(
            claimed,
            NodeCommitKind.Continue,
            replacement,
            new NextWork(transition.TargetId, MillisecondUtcNow()),
            null,
            outbox,
            token).ConfigureAwait(false);
    }

    private Task CommitFailureAsync(
        ClaimedWork claimed,
        TechnicalId error,
        CancellationToken token,
        NodeExecutionDisposition disposition = NodeExecutionDisposition.RetryableFailure)
    {
        if (disposition is NodeExecutionDisposition.RetryableFailure && claimed.Attempt < _options.MaximumAttempts)
        {
            var delay = _retry.GetDelay(claimed.Attempt, _options.RetryBaseDelay, _options.RetryMaximumDelay);
            return CommitAsync(claimed, NodeCommitKind.Retry, null,
                new NextWork(claimed.Lease.NodeId, MillisecondUtcNow() + delay), error.Value, [], token);
        }

        return CommitAsync(claimed, NodeCommitKind.Fail, null, null, error.Value, [], token);
    }

    private async Task CommitAsync(
        ClaimedWork claimed,
        NodeCommitKind kind,
        WorkflowState? replacement,
        NextWork? nextWork,
        string? errorCode,
        IReadOnlyList<OutboxWrite> outbox,
        CancellationToken token)
    {
        var result = await _store.CommitNodeResultAsync(new CommitNodeResultCommand(
            claimed.Lease.WorkItemId, claimed.Lease.Token, claimed.Lease.Generation,
            claimed.Lease.InstanceRevision, kind, replacement, nextWork, errorCode, outbox), token).ConfigureAwait(false);
        if (result.Outcome is not StoreOutcome.Succeeded)
            throw new WorkflowRuntimeException(result.ErrorCode ?? "EW4091_COMMIT_FAILED", "The workflow result was not committed.");
    }

    private async Task RenewLeaseAsync(
        WorkLease original,
        CancellationTokenSource handlerCancellation,
        CancellationTokenSource heartbeatStop,
        TaskCompletionSource leaseLost,
        CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_options.LeaseRenewalInterval, _timeProvider, heartbeatStop.Token).ConfigureAwait(false);
                var result = await _store.RenewLeaseAsync(new RenewLeaseCommand(
                    original.WorkItemId, original.Token, original.Generation, _options.LeaseDuration), stoppingToken).ConfigureAwait(false);
                if (result.Outcome is not StoreOutcome.Succeeded)
                {
                    leaseLost.TrySetResult();
                    await handlerCancellation.CancelAsync().ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (heartbeatStop.IsCancellationRequested || stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static async Task IgnoreHeartbeatCancellationAsync(Task heartbeat)
    {
        try { await heartbeat.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    private DateTimeOffset MillisecondUtcNow() =>
        DateTimeOffset.FromUnixTimeMilliseconds(_timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
}

/// <summary>Stable runtime failure carrying a machine-readable code.</summary>
public sealed class WorkflowRuntimeException(string code, string message) : InvalidOperationException($"{code}: {message}")
{
    public string Code { get; } = code;
}

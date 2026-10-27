using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;
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
    private readonly IWorkflowStateValidationService? _stateValidation;
    private readonly IWorkflowStore _store;
    private readonly IScopedWorkflowHandlerResolver _resolver;
    private readonly WorkflowRuntimeOptions _options;
    private readonly IWorkflowRetryDelayStrategy _retry;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkflowAuthorizationService? _authorization;

    private readonly EnterpriseWorkflow.Core.Serialization.IWorkflowSerializer _serializer;

    public WorkflowExecutionPump(
        IWorkflowStore store,
        IScopedWorkflowHandlerResolver resolver,
        IOptions<WorkflowRuntimeOptions> options,
        IWorkflowRetryDelayStrategy retry,
        TimeProvider timeProvider,
        IWorkflowAuthorizationService? authorization = null,
        IWorkflowStateValidationService? stateValidation = null, EnterpriseWorkflow.Core.Serialization.IWorkflowSerializer? serializer = null)
    {
        _serializer = serializer ?? new EnterpriseWorkflow.Core.Serialization.WorkflowSerializer(options.Value.Serialization);
        _stateValidation = stateValidation;
        _store = store;
        _resolver = resolver;
        _options = options.Value;
        _options.Validate();
        _retry = retry;
        _timeProvider = timeProvider;
        _authorization = authorization;
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
        if (node.Role is WorkflowNodeRole.HumanTask)
        {
            await CommitHumanWaitAsync(claimed, node, stoppingToken).ConfigureAwait(false);
            return;
        }
        if (node.Role is WorkflowNodeRole.Timer)
        {
            await CommitTimerWaitAsync(claimed, node, stoppingToken).ConfigureAwait(false);
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
        catch (WorkflowMediaInspectionPendingException)
        {
            heartbeatStop.Cancel();
            await IgnoreHeartbeatCancellationAsync(heartbeat).ConfigureAwait(false);
            if (!leaseLost.Task.IsCompleted && !stoppingToken.IsCancellationRequested)
                await CommitFailureAsync(claimed, new("runtime.media-inspection-pending"), stoppingToken).ConfigureAwait(false);
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
        var context = CreateContext(claimed, node);
        try
        {
            var result = await scope.Handler.ExecuteAsync(context, token).ConfigureAwait(false) ?? throw new InvalidOperationException("A service handler returned null.");
            var media = context.MediaExecution!.Seal();
            return result.Disposition == NodeExecutionDisposition.Succeeded ? result.WithCollectedMedia(media.Attachments, media.Deletions) : result;
        }
        catch { try { context.MediaExecution!.Seal(); } catch (InvalidOperationException) { } throw; }
    }

    private async Task<DecisionNodeResult> InvokeDecisionAsync(ClaimedWork claimed, WorkflowNode node, CancellationToken token)
    {
        await using var scope = _resolver.ResolveDecision(node.Handler!);
        var context = CreateContext(claimed, node);
        try
        {
            var result = await scope.Handler.EvaluateAsync(context, token).ConfigureAwait(false) ?? throw new InvalidOperationException("A decision handler returned null.");
            var media = context.MediaExecution!.Seal();
            if (media.Attachments.Length > 0 || media.Deletions.Length > 0) throw new InvalidOperationException("Decision handlers cannot write media.");
            return result;
        }
        catch { try { context.MediaExecution!.Seal(); } catch (InvalidOperationException) { } throw; }
    }

    private static NodeExecutionContext CreateContext(ClaimedWork claimed, WorkflowNode node) => new(
        claimed.Lease.InstanceId, claimed.Lease.ActivationId, node.Id, claimed.Attempt, claimed.State, node.Configuration)
        { MediaExecution = WorkflowMediaSession.Create(claimed.Definition, node.Configuration, new(new(new("system"), "workflow"), claimed.Lease.InstanceId, claimed.Lease.ActivationId)) };

    private async Task CommitHumanWaitAsync(ClaimedWork claimed, WorkflowNode node, CancellationToken token)
    {
        var policy = WorkflowStagePolicy.Read(node.Configuration);
        var contract = node.HumanTask!;
        ActorIdentity? assignee = policy?.AssignedToInitiator == true ? claimed.Initiator : claimed.PreparedAssignee;
        if (policy is not null && assignee is not null)
        {
            var eligible = false;
            if ((contract.AllowInitiator || assignee != claimed.Initiator) && _authorization is not null)
            {
                foreach (var action in contract.Actions)
                {
                    var decision = await _authorization.AuthorizeAsync(new(new(assignee.ProviderId.Value, assignee.SubjectId),
                        new(claimed.Definition.Id.Value, claimed.Definition.Version, node.Id.Value, new(action.ActionId.Value))), token).ConfigureAwait(false);
                    if (decision.Allowed) { eligible = true; break; }
                }
            }
            if (!eligible) assignee = null;
        }
        IReadOnlyList<OutboxWrite> messages = [];
        if (_options.NotifyHumanTaskPools && policy is not null && contract.AssignmentMode == HumanTaskAssignmentMode.EligiblePool)
            messages = [new(new("pool-notification"), new("workflow.pool-notification"), "application/json", "{}")];
        await CommitAsync(claimed, NodeCommitKind.WaitHumanTask, null, null,
            policy is not null && contract.AssignmentMode == HumanTaskAssignmentMode.DesignatedIdentity && assignee is null
                ? "runtime.task-awaiting-eligible-assignment" : null,
            messages, token, new HumanTaskWait(contract.AssignmentMode, policy is not null, assignee)).ConfigureAwait(false);
    }

    private Task CommitTimerWaitAsync(ClaimedWork claimed, WorkflowNode node, CancellationToken token)
    {
        var transition = claimed.Definition.Transitions.Single(item => item.SourceId == node.Id);
        return CommitAsync(claimed, NodeCommitKind.WaitTimer, null, null, null, [], token,
            timer: new TimerWait(node.Timer!.Delay, transition.TargetId));
    }

    private async Task CommitServiceResultAsync(
        ClaimedWork claimed, WorkflowNode node, ServiceNodeResult result, CancellationToken token)
    {
        if (result.Disposition is NodeExecutionDisposition.Succeeded)
        {
            var outbox = result.Effects.Select(item => new OutboxWrite(
                item.OperationId, item.Destination, item.ContentType, item.Payload.CanonicalText)).ToArray();
            await CommitSuccessAsync(claimed, node, null, result.StateUpdate, outbox, token, result.Media, result.MediaDeletes).ConfigureAwait(false);
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
        CancellationToken token,
        IReadOnlyList<WorkflowMediaReference>? media = null, IReadOnlyList<WorkflowMediaReference>? mediaDeletes = null)
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

        if (!_serializer.IsValidState((replacement ?? claimed.State).Value))
        { await CommitFailureAsync(claimed, new("runtime.state-invalid"), token, NodeExecutionDisposition.PermanentFailure).ConfigureAwait(false); return; }
        var stateContract = _stateValidation?.GetContract(claimed.Definition, claimed.State.SchemaVersion) ?? WorkflowStateContract.From(claimed.Definition);
        if (stateContract is not null && (stateContract.Version != claimed.State.SchemaVersion || stateContract.Validate((replacement ?? claimed.State).Value, WorkflowStagePolicy.Read(node.Configuration)?.StateValidationGroups).Count > 0))
        {
            await CommitFailureAsync(claimed, new("runtime.state-invalid"), token, NodeExecutionDisposition.PermanentFailure).ConfigureAwait(false);
            return;
        }
        if ((stateContract?.Validators?.Length ?? 0) > 0 && (_stateValidation is null || !await _stateValidation.ValidateAsync(claimed.Definition,
            new(replacement ?? claimed.State, node.Id.Value, outcome?.Value, WorkflowStagePolicy.Read(node.Configuration)?.StateValidationGroups ?? []), token)))
        {
            await CommitFailureAsync(claimed, new("runtime.state-invalid"), token, NodeExecutionDisposition.PermanentFailure).ConfigureAwait(false);
            return;
        }
        await CommitAsync(
            claimed,
            NodeCommitKind.Continue,
            replacement,
            new NextWork(transition.TargetId, MillisecondUtcNow()),
            null,
            outbox,
            token, media: media, mediaDeletes: mediaDeletes).ConfigureAwait(false);
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
        CancellationToken token,
        HumanTaskWait? humanTask = null,
        TimerWait? timer = null,
        IReadOnlyList<WorkflowMediaReference>? media = null, IReadOnlyList<WorkflowMediaReference>? mediaDeletes = null)
    {
        var result = await _store.CommitNodeResultAsync(new CommitNodeResultCommand(
            claimed.Lease.WorkItemId, claimed.Lease.Token, claimed.Lease.Generation,
            claimed.Lease.InstanceRevision, kind, replacement, nextWork, errorCode, outbox, humanTask, timer, media, mediaDeletes), token).ConfigureAwait(false);
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

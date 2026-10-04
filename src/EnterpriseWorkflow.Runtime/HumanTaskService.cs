using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Runtime;

/// <summary>One authenticated human-task command outcome.</summary>
public sealed record HumanTaskCommandResult(
    bool Succeeded,
    bool WasReplay,
    HumanTaskStatus? Status,
    long? TaskRevision,
    long? InstanceRevision,
    string? ErrorCode = null);

/// <summary>Authorization-filtered inbox page.</summary>
public sealed record HumanTaskInboxPage(IReadOnlyList<OpenHumanTask> Tasks, HumanTaskCursor? Next, int Scanned);

/// <summary>Authenticated application boundary for task assignment, claim, completion and inbox reads.</summary>
public interface IHumanTaskService
{
    ValueTask<HumanTaskCommandResult> AssignAsync(HumanTaskId taskId, long expectedRevision,
        IdentityReference actor, IdentityReference assignee, CancellationToken cancellationToken);
    ValueTask<HumanTaskCommandResult> ClaimAsync(HumanTaskId taskId, long expectedRevision,
        IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<HumanTaskCommandResult> ReleaseAsync(HumanTaskId taskId, long expectedRevision,
        IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<HumanTaskCommandResult> CompleteAsync(HumanTaskId taskId, long expectedRevision,
        IdentityReference actor, WorkflowActionId action, TechnicalId idempotencyKey,
        CanonicalJson submission, CancellationToken cancellationToken);
    ValueTask<HumanTaskInboxPage> ReadInboxAsync(IdentityReference actor, HumanTaskCursor? after,
        CancellationToken cancellationToken);
}

internal sealed class HumanTaskService(
    IWorkflowStore store,
    IWorkflowAuthorizationService authorization,
    IScopedWorkflowHandlerResolver resolver,
    IOptions<WorkflowRuntimeOptions> configuredOptions) : IHumanTaskService
{
    private readonly WorkflowRuntimeOptions _options = Validate(configuredOptions.Value);

    public async ValueTask<HumanTaskCommandResult> AssignAsync(
        HumanTaskId taskId, long expectedRevision, IdentityReference actor, IdentityReference assignee,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (loaded.Snapshot is null) return loaded.Error!;
        var snapshot = loaded.Snapshot;
        if (!await IsAllowedAsync(actor, snapshot, WorkflowActions.AssignTask, cancellationToken).ConfigureAwait(false))
            return Denied("security.task-assign-denied");
        if (!await HasAnyTaskActionAsync(assignee, snapshot, cancellationToken).ConfigureAwait(false))
            return Denied("security.task-assignee-ineligible");
        var result = await store.AssignHumanTaskAsync(new AssignHumanTaskCommand(
            taskId, expectedRevision, ToActor(assignee), ToActor(actor)), cancellationToken).ConfigureAwait(false);
        return Mutation(result);
    }

    public async ValueTask<HumanTaskCommandResult> ClaimAsync(
        HumanTaskId taskId, long expectedRevision, IdentityReference actor, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (loaded.Snapshot is null) return loaded.Error!;
        if (!await IsAllowedAsync(actor, loaded.Snapshot, WorkflowActions.ClaimTask, cancellationToken).ConfigureAwait(false) ||
            !await HasAnyTaskActionAsync(actor, loaded.Snapshot, cancellationToken).ConfigureAwait(false))
            return Denied("security.task-claim-denied");
        return Mutation(await store.ClaimHumanTaskAsync(
            new ClaimHumanTaskCommand(taskId, expectedRevision, ToActor(actor)), cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask<HumanTaskCommandResult> ReleaseAsync(
        HumanTaskId taskId, long expectedRevision, IdentityReference actor, CancellationToken cancellationToken) =>
        Mutation(await store.ReleaseHumanTaskAsync(
            new ReleaseHumanTaskCommand(taskId, expectedRevision, ToActor(actor)), cancellationToken).ConfigureAwait(false));

    public async ValueTask<HumanTaskCommandResult> CompleteAsync(
        HumanTaskId taskId, long expectedRevision, IdentityReference actor, WorkflowActionId action,
        TechnicalId idempotencyKey, CanonicalJson submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.CanonicalByteCount > _options.HumanTaskMaximumSubmissionBytes)
            return Denied("runtime.human-task-submission-too-large");
        var actorValue = ToActor(actor);
        var actionId = new TechnicalId(action.Value);
        var requestHash = ComputeRequestHash(actionId, submission);
        var previous = await store.GetHumanTaskCompletionReceiptAsync(taskId, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (previous.Outcome is StoreOutcome.Succeeded && previous.Value is not null)
        {
            var receipt = previous.Value;
            return receipt.Actor == actorValue && receipt.ActionId == actionId &&
                   string.Equals(receipt.RequestSha256, requestHash, StringComparison.Ordinal)
                ? new(true, true, HumanTaskStatus.Completed, receipt.TaskRevision, receipt.InstanceRevision)
                : Denied("runtime.human-task-idempotency-conflict");
        }

        var loaded = await LoadAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (loaded.Snapshot is null) return loaded.Error!;
        var snapshot = loaded.Snapshot;
        var node = snapshot.Definition.Nodes.Single(item => item.Id == snapshot.NodeId);
        var contract = node.HumanTask!;
        var declaredAction = contract.Actions.SingleOrDefault(item => item.ActionId == actionId);
        if (declaredAction is null) return Denied("runtime.human-task-action-not-declared");
        if (!await IsAllowedAsync(actor, snapshot, action, cancellationToken).ConfigureAwait(false))
            return Denied("security.task-action-denied");
        if (!IsSame(snapshot.Assignee, actorValue)) return Denied("runtime.human-task-not-assignee");
        if (!contract.AllowInitiator && IsSame(snapshot.Initiator, actorValue))
            return Denied("runtime.human-task-initiator-forbidden");
        if (contract.DistinctFrom.Any(constraint => snapshot.CompletionHistory.Any(history =>
            history.NodeId == constraint.NodeId && history.ActionId == constraint.ActionId && IsSame(history.Actor, actorValue))))
            return Denied("runtime.human-task-separation-violated");

        await using var scope = resolver.ResolveHumanTask(contract.CompletionHandler);
        var handlerResult = await scope.Handler.CompleteAsync(new HumanTaskCompletionContext(
            taskId, snapshot.InstanceId, snapshot.ActivationId, snapshot.NodeId, actorValue,
            actionId, submission, snapshot.State, node.Configuration), cancellationToken).ConfigureAwait(false);
        if (!handlerResult.Succeeded)
            return Denied(handlerResult.ErrorCode?.Value ?? "runtime.human-task-submission-rejected");
        if (handlerResult.Outcome != declaredAction.Outcome)
            return Denied("runtime.human-task-handler-outcome-mismatch");
        foreach (var assignment in handlerResult.DesignatedAssignments)
        {
            var target = snapshot.Definition.Nodes.SingleOrDefault(candidate => candidate.Id == assignment.NodeId);
            if (target?.HumanTask is not { AssignmentMode: HumanTaskAssignmentMode.DesignatedIdentity })
                return Denied("runtime.designated-assignment-node-invalid");
            if (!await HasAnyTaskActionAsync(ToIdentity(assignment.Assignee), snapshot.Definition, target, cancellationToken)
                    .ConfigureAwait(false))
                return Denied("security.task-assignee-ineligible");
        }
        var transition = snapshot.Definition.Transitions.SingleOrDefault(item =>
            item.SourceId == node.Id && item.Outcome == handlerResult.Outcome);
        if (transition is null) return Denied("runtime.human-task-transition-not-found");

        WorkflowState? replacement = null;
        if (handlerResult.StateUpdate.ReplacesState)
            replacement = WorkflowState.Create(handlerResult.StateUpdate.GetReplacement()!.Value.GetRawText(), snapshot.State.SchemaVersion);
        var outbox = handlerResult.Effects.Select(item => new OutboxWrite(
            item.OperationId, item.Destination, item.ContentType, item.Payload.CanonicalText)).ToArray();
        var completed = await store.CompleteHumanTaskAsync(new CompleteHumanTaskCommand(
            taskId, expectedRevision, snapshot.InstanceRevision, actorValue, actionId, idempotencyKey, requestHash,
            replacement, new NextWork(transition.TargetId, MillisecondUtcNow()), outbox,
            handlerResult.DesignatedAssignments), cancellationToken).ConfigureAwait(false);
        return completed.Outcome switch
        {
            StoreOutcome.Succeeded => new(true, false, HumanTaskStatus.Completed,
                completed.Value!.TaskRevision, completed.Value.InstanceRevision),
            StoreOutcome.Idempotent => new(true, true, HumanTaskStatus.Completed,
                completed.Value!.TaskRevision, completed.Value.InstanceRevision),
            _ => Denied(completed.ErrorCode ?? "runtime.human-task-completion-conflict"),
        };
    }

    public async ValueTask<HumanTaskInboxPage> ReadInboxAsync(
        IdentityReference actor, HumanTaskCursor? after, CancellationToken cancellationToken)
    {
        var accepted = new List<OpenHumanTask>(_options.HumanTaskInboxPageSize);
        var scanned = 0;
        var cursor = after;
        while (accepted.Count < _options.HumanTaskInboxPageSize && scanned < _options.HumanTaskInboxMaximumScan)
        {
            var take = Math.Min(_options.HumanTaskInboxPageSize, _options.HumanTaskInboxMaximumScan - scanned);
            var page = await store.ReadOpenHumanTasksAsync(new ReadOpenHumanTasksCommand(cursor, take), cancellationToken).ConfigureAwait(false);
            if (page.Outcome is not StoreOutcome.Succeeded || page.Value is null) break;
            foreach (var task in page.Value.Tasks)
            {
                scanned++;
                cursor = new HumanTaskCursor(task.CreatedAtUtc.ToUnixTimeMilliseconds(), task.TaskId);
                var exactAssignee = task.Assignee is not null && IsSame(task.Assignee, ToActor(actor));
                if ((task.Status is HumanTaskStatus.Assigned or HumanTaskStatus.Claimed) && !exactAssignee) continue;
                var snapshot = await store.GetHumanTaskAsync(task.TaskId, cancellationToken).ConfigureAwait(false);
                if (snapshot.Value is not null &&
                    await IsAllowedAsync(actor, snapshot.Value, WorkflowActions.ReadTask, cancellationToken).ConfigureAwait(false) &&
                    (exactAssignee || await HasAnyTaskActionAsync(actor, snapshot.Value, cancellationToken).ConfigureAwait(false)))
                    accepted.Add(task);
                if (accepted.Count == _options.HumanTaskInboxPageSize) break;
            }

            if (accepted.Count == _options.HumanTaskInboxPageSize) break;
            if (page.Value.Tasks.Count < take) break;
        }

        return new HumanTaskInboxPage(accepted, cursor, scanned);
    }

    private async ValueTask<(HumanTaskSnapshot? Snapshot, HumanTaskCommandResult? Error)> LoadAsync(
        HumanTaskId taskId, CancellationToken cancellationToken)
    {
        var result = await store.GetHumanTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        return result.Outcome is StoreOutcome.Succeeded && result.Value is not null
            ? (result.Value, null)
            : (null, Denied(result.ErrorCode ?? "runtime.human-task-not-found"));
    }

    private async ValueTask<bool> HasAnyTaskActionAsync(
        IdentityReference identity, HumanTaskSnapshot snapshot, CancellationToken cancellationToken)
    {
        var node = snapshot.Definition.Nodes.Single(item => item.Id == snapshot.NodeId);
        return await HasAnyTaskActionAsync(identity, snapshot.Definition, node, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> HasAnyTaskActionAsync(
        IdentityReference identity, WorkflowDefinition definition, WorkflowNode node, CancellationToken cancellationToken)
    {
        foreach (var action in node.HumanTask!.Actions)
        {
            if (await IsAllowedAsync(identity, definition, node.Id.Value,
                    new WorkflowActionId(action.ActionId.Value), cancellationToken).ConfigureAwait(false))
                return true;
        }
        return false;
    }

    private async ValueTask<bool> IsAllowedAsync(
        IdentityReference identity, HumanTaskSnapshot snapshot, WorkflowActionId action, CancellationToken cancellationToken)
        => await IsAllowedAsync(identity, snapshot.Definition, snapshot.NodeId.Value, action, cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<bool> IsAllowedAsync(
        IdentityReference identity, WorkflowDefinition definition, string? nodeId,
        WorkflowActionId action, CancellationToken cancellationToken)
    {
        var scope = new WorkflowAuthorizationScope(definition.Id.Value, definition.Version, nodeId, action);
        var decision = await authorization.AuthorizeAsync(new WorkflowAuthorizationRequest(identity, scope), cancellationToken)
            .ConfigureAwait(false);
        return decision.Allowed;
    }

    private static ActorIdentity ToActor(IdentityReference identity) =>
        new(new TechnicalId(identity.ProviderId), identity.SubjectId);
    private static IdentityReference ToIdentity(ActorIdentity actor) =>
        new(actor.ProviderId.Value, actor.SubjectId);
    private static bool IsSame(ActorIdentity? left, ActorIdentity right) => left is not null && left == right;
    private static HumanTaskCommandResult Denied(string code) => new(false, false, null, null, null, code);
    private static HumanTaskCommandResult Mutation(StoreResult<HumanTaskMutationResult> result) =>
        result.Outcome is StoreOutcome.Succeeded && result.Value is not null
            ? new(true, false, result.Value.Status, result.Value.Revision, result.Value.InstanceRevision)
            : Denied(result.ErrorCode ?? "runtime.human-task-mutation-conflict");
    private static DateTimeOffset MillisecondUtcNow() =>
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    private static WorkflowRuntimeOptions Validate(WorkflowRuntimeOptions options) { options.Validate(); return options; }

    private static string ComputeRequestHash(TechnicalId action, CanonicalJson submission)
    {
        using var stream = new MemoryStream();
        Write(stream, action.Value);
        Write(stream, submission.CanonicalText);
        return Convert.ToHexStringLower(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }

    private static void Write(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, bytes.Length);
        stream.Write(size);
        stream.Write(bytes);
    }
}

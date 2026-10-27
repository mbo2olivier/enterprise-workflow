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

/// <summary>Authorized server-side task snapshot for application services.</summary>
public sealed record HumanTaskReadResult(bool Succeeded, HumanTaskSnapshot? Snapshot, string? ErrorCode = null);

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
    ValueTask<HumanTaskReadResult> ReadAsync(HumanTaskId taskId, IdentityReference actor,
        CancellationToken cancellationToken);
}

internal sealed class HumanTaskService(
    IWorkflowStore store,
    IWorkflowAuthorizationService authorization,
    IScopedWorkflowHandlerResolver resolver,
    IOptions<WorkflowRuntimeOptions> configuredOptions,
    IWorkflowStateValidationService? stateValidation = null, IIdentityDisplayNameResolver? names = null, EnterpriseWorkflow.Core.Serialization.IWorkflowSerializer? serializer = null) : IHumanTaskService
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
        if (WorkflowStagePolicy.Read(snapshot.Definition.Nodes.Single(node => node.Id == snapshot.NodeId).Configuration)?.AssignedToInitiator == true && ToActor(assignee) != snapshot.Initiator)
            return Denied("runtime.initiator-assignment-required");
        if (!await HasAnyTaskActionAsync(assignee, snapshot, cancellationToken).ConfigureAwait(false))
            return Denied("security.task-assignee-ineligible");
        var result = await store.AssignHumanTaskAsync(new AssignHumanTaskCommand(
            taskId, expectedRevision, ToActor(assignee), await AuditActorAsync(actor, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false);
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
            new ClaimHumanTaskCommand(taskId, expectedRevision, await AuditActorAsync(actor, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask<HumanTaskCommandResult> ReleaseAsync(
        HumanTaskId taskId, long expectedRevision, IdentityReference actor, CancellationToken cancellationToken) =>
        Mutation(await store.ReleaseHumanTaskAsync(
            new ReleaseHumanTaskCommand(taskId, expectedRevision, await AuditActorAsync(actor, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false));

    public async ValueTask<HumanTaskCommandResult> CompleteAsync(
        HumanTaskId taskId, long expectedRevision, IdentityReference actor, WorkflowActionId action,
        TechnicalId idempotencyKey, CanonicalJson submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.CanonicalByteCount > _options.HumanTaskMaximumSubmissionBytes)
            return Denied("runtime.human-task-submission-too-large");
        var actorValue = await AuditActorAsync(actor, cancellationToken).ConfigureAwait(false);
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

        var policy = WorkflowStagePolicy.Read(node.Configuration);
        CanonicalJson businessSubmission;
        IReadOnlyList<DesignatedTaskAssignment> submittedAssignments;
        try
        {
            (businessSubmission, submittedAssignments) = HumanTaskSubmission.Read(submission);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return Denied("runtime.invalid-assignment-submission");
        }
        WorkflowActionPolicy? actionPolicy = null;
        if (policy is not null)
        {
            if (!policy.Actions.TryGetValue(actionId.Value, out actionPolicy)) return Denied("runtime.action-policy-missing");
            if (businessSubmission.ToJsonElement().TryGetProperty("comment", out var commentValue) &&
                commentValue.ValueKind != System.Text.Json.JsonValueKind.Null &&
                (commentValue.ValueKind != System.Text.Json.JsonValueKind.String || commentValue.GetString()!.Length > 4000))
                return Denied("runtime.comment-invalid");
            var expected = actionPolicy.AssigneeTarget;
            if (expected is null ? submittedAssignments.Count != 0 :
                submittedAssignments.Count != 1 || submittedAssignments[0].NodeId.Value != expected)
                return Denied("runtime.required-assignment-mismatch");
            if (actionPolicy.RequireComment &&
                (!businessSubmission.ToJsonElement().TryGetProperty("comment", out var comment) ||
                 comment.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(comment.GetString())))
                return Denied("runtime.comment-required");
        }
        else if (submittedAssignments.Count != 0) return Denied("runtime.undeclared-assignment");

        await using var scope = resolver.ResolveHumanTask(contract.CompletionHandler);
        var mediaExecution = WorkflowMediaSession.Create(snapshot.Definition, node.Configuration, new(actorValue, snapshot.InstanceId, snapshot.ActivationId), businessSubmission);
        var handlerContext = new HumanTaskCompletionContext(taskId, snapshot.InstanceId, snapshot.ActivationId, snapshot.NodeId, actorValue,
            actionId, businessSubmission, snapshot.State, node.Configuration) { MediaExecution = mediaExecution, CompletionAtUtc = MillisecondUtcNow() };
        HumanTaskCompletionResult handlerResult;
        try { handlerResult = await scope.Handler.CompleteAsync(handlerContext, cancellationToken).ConfigureAwait(false); }
        catch (WorkflowMediaInspectionPendingException) { mediaExecution.Seal(); return Denied("runtime.media-inspection-pending"); }
        catch { mediaExecution.Seal(); throw; }
        var collectedMedia = mediaExecution.Seal();
        if (!handlerResult.Succeeded)
            return Denied(handlerResult.ErrorCode?.Value ?? "runtime.human-task-submission-rejected");
        var assignments = policy is null ? handlerResult.DesignatedAssignments.ToArray() : submittedAssignments.ToArray();
        if (policy is not null && handlerResult.DesignatedAssignments.Any(assignment => !assignments.Contains(assignment)))
            return Denied("runtime.handler-assignment-mismatch");
        foreach (var assignment in assignments)
        {
            var target = snapshot.Definition.Nodes.SingleOrDefault(candidate => candidate.Id == assignment.NodeId);
            if (target?.HumanTask is not { AssignmentMode: HumanTaskAssignmentMode.DesignatedIdentity } ||
                (policy is not null && (assignment.NodeId.Value != actionPolicy?.AssigneeTarget ||
                    !snapshot.Definition.Transitions.Any(edge => edge.SourceId == node.Id && edge.Outcome == declaredAction.Outcome && edge.TargetId == assignment.NodeId))))
                return Denied("runtime.designated-assignment-node-invalid");
            if ((!target.HumanTask.AllowInitiator && assignment.Assignee == snapshot.Initiator) ||
                target.HumanTask.DistinctFrom.Any(constraint =>
                    (constraint.NodeId == node.Id && constraint.ActionId == actionId && assignment.Assignee == actorValue) ||
                    snapshot.CompletionHistory.Any(history => history.NodeId == constraint.NodeId && history.ActionId == constraint.ActionId && history.Actor == assignment.Assignee)) ||
                !await HasAnyTaskActionAsync(ToIdentity(assignment.Assignee), snapshot.Definition, target, cancellationToken)
                    .ConfigureAwait(false))
                return Denied("security.task-assignee-ineligible");
        }
        var transition = snapshot.Definition.Transitions.SingleOrDefault(item =>
            item.SourceId == node.Id && item.Outcome == declaredAction.Outcome);
        if (transition is null) return Denied("runtime.human-task-transition-not-found");

        WorkflowState? replacement = null;
        if (handlerResult.StateUpdate.ReplacesState)
        {
            try { replacement = WorkflowState.Create(handlerResult.StateUpdate.GetReplacement()!.Value.GetRawText(), snapshot.State.SchemaVersion); }
            catch (FormatException) { return Denied("runtime.state-invalid"); }
        }
        var stateContract = stateValidation?.GetContract(snapshot.Definition, snapshot.State.SchemaVersion) ?? WorkflowStateContract.From(snapshot.Definition);
        var validationGroups = (policy?.StateValidationGroups ?? []).Concat(actionPolicy?.StateValidationGroups ?? []).ToArray();
        if (!(serializer ?? new EnterpriseWorkflow.Core.Serialization.WorkflowSerializer(_options.Serialization)).IsValidState((replacement ?? snapshot.State).Value)) return Denied("runtime.state-invalid");
        if (stateContract is not null && (stateContract.Version != snapshot.State.SchemaVersion || stateContract.Validate((replacement ?? snapshot.State).Value, validationGroups).Count > 0))
            return Denied("runtime.state-invalid");
        if ((stateContract?.Validators?.Length ?? 0) > 0 && (stateValidation is null || !await stateValidation.ValidateAsync(snapshot.Definition,
            new(replacement ?? snapshot.State, node.Id.Value, actionId.Value, validationGroups), cancellationToken))) return Denied("runtime.state-invalid");
        var outbox = handlerResult.Effects.Select(item => new OutboxWrite(
            item.OperationId, item.Destination, item.ContentType, item.Payload.CanonicalText)).ToArray();
        var completed = await store.CompleteHumanTaskAsync(new CompleteHumanTaskCommand(
            taskId, expectedRevision, snapshot.InstanceRevision, actorValue, actionId, idempotencyKey, requestHash,
            replacement, new NextWork(transition.TargetId, MillisecondUtcNow()), outbox,
            assignments, HumanTaskSubmission.ReadMedia(submission).Concat(collectedMedia.Attachments).Distinct().ToArray(), collectedMedia.Deletions), cancellationToken).ConfigureAwait(false);
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

    public async ValueTask<HumanTaskReadResult> ReadAsync(
        HumanTaskId taskId,
        IdentityReference actor,
        CancellationToken cancellationToken)
    {
        var result = await store.GetHumanTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (result.Outcome is not StoreOutcome.Succeeded || result.Value is null)
            return new(false, null, result.ErrorCode ?? "runtime.human-task-not-found");
        var snapshot = result.Value;
        var exactAssignee = snapshot.Assignee is not null && IsSame(snapshot.Assignee, ToActor(actor));
        if (!await IsAllowedAsync(actor, snapshot, WorkflowActions.ReadTask, cancellationToken).ConfigureAwait(false) ||
            (!exactAssignee && !await HasAnyTaskActionAsync(actor, snapshot, cancellationToken).ConfigureAwait(false)))
            return new(false, null, "security.task-read-denied");
        return new(true, snapshot);
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
        var actor = ToActor(identity);
        if ((!node.HumanTask!.AllowInitiator && snapshot.Initiator == actor) ||
            node.HumanTask.DistinctFrom.Any(constraint => snapshot.CompletionHistory.Any(history => history.NodeId == constraint.NodeId && history.ActionId == constraint.ActionId && history.Actor == actor))) return false;
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

    private async ValueTask<ActorIdentity> AuditActorAsync(IdentityReference actor, CancellationToken token) =>
        ToActor(actor) with { DisplayName = names is null ? actor.SubjectId : await names.ResolveAsync(actor, token).ConfigureAwait(false) };
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

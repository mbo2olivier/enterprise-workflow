using System.Data;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;

namespace EnterpriseWorkflow.Persistence.Oracle;

public sealed partial class OracleWorkflowStore
{
    public async ValueTask<StoreResult<HumanTaskSnapshot>> GetHumanTaskAsync(
        HumanTaskId taskId, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var task = await context.HumanTasks.FindAsync([FormatGuid(taskId.Value)], cancellationToken).ConfigureAwait(false);
            if (task is null) return StoreResults.NotFound<HumanTaskSnapshot>("EW3035_HUMAN_TASK_NOT_FOUND");
            var instance = await context.Instances.FindAsync([task.InstanceId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The human task has no instance.");
            var definitionRow = await context.Definitions.FindAsync(
                [instance.DefinitionId, instance.DefinitionVersion], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The human task has no definition.");
            var history = await context.HumanTasks
                .Where(row => row.InstanceId == task.InstanceId && row.Status == (int)HumanTaskStatus.Completed)
                .OrderBy(row => row.CompletedAtUnixMilliseconds).ThenBy(row => row.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var definition = PublishedWorkflowDefinitionReader.Read(
                definitionRow.CanonicalJson, definitionRow.DefinitionId, definitionRow.Version, definitionRow.Sha256);
            return StoreResults.Succeeded(ToSnapshot(task, instance, definition, history));
        }, cancellationToken).ConfigureAwait(false);

    public ValueTask<StoreResult<HumanTaskMutationResult>> AssignHumanTaskAsync(
        AssignHumanTaskCommand command, CancellationToken cancellationToken) => MutateHumanTaskAsync(
        command.TaskId, command.ExpectedRevision, HumanTaskStatus.AwaitingAssignment, HumanTaskStatus.Assigned,
        command.AssignedBy, command.Assignee, false, "HumanTaskAssigned", cancellationToken);

    public ValueTask<StoreResult<HumanTaskMutationResult>> ClaimHumanTaskAsync(
        ClaimHumanTaskCommand command, CancellationToken cancellationToken) => MutateHumanTaskAsync(
        command.TaskId, command.ExpectedRevision, HumanTaskStatus.Available, HumanTaskStatus.Claimed,
        command.Actor, command.Actor, false, "HumanTaskClaimed", cancellationToken);

    public ValueTask<StoreResult<HumanTaskMutationResult>> ReleaseHumanTaskAsync(
        ReleaseHumanTaskCommand command, CancellationToken cancellationToken) => MutateHumanTaskAsync(
        command.TaskId, command.ExpectedRevision, HumanTaskStatus.Claimed, HumanTaskStatus.Available,
        command.Actor, null, true, "HumanTaskReleased", cancellationToken);

    public async ValueTask<StoreResult<HumanTaskCompletionReceipt>> GetHumanTaskCompletionReceiptAsync(
        HumanTaskId taskId, TechnicalId idempotencyKey, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var row = await context.HumanTaskReceipts.FindAsync(
                [StoreContractRules.ComputeHumanTaskReceiptKey(taskId, idempotencyKey)], cancellationToken).ConfigureAwait(false);
            return row is null ? StoreResults.NotFound<HumanTaskCompletionReceipt>("EW3036_HUMAN_TASK_RECEIPT_NOT_FOUND")
                : StoreResults.Succeeded(ToReceipt(row));
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<CompleteHumanTaskResult>> CompleteHumanTaskAsync(
        CompleteHumanTaskCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!StoreContractRules.IsSha256(command.RequestSha256) || !IsUtcMillisecond(command.NextWork.DueAtUtc) ||
            (command.Outbox?.Any(item => !StoreContractRules.IsValidOutboxWrite(item)) ?? false) ||
            (command.Outbox?.GroupBy(item => item.OperationId.Value, StringComparer.Ordinal).Any(group => group.Count() > 1) ?? false) ||
            (command.DesignatedAssignments ?? []).GroupBy(item => item.NodeId.Value, StringComparer.Ordinal).Any(group => group.Count() > 1))
            return StoreResults.Conflict<CompleteHumanTaskResult>("EW3037_INVALID_HUMAN_TASK_COMPLETION");

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var taskKey = FormatGuid(command.TaskId.Value);
            if (!await LockHumanTaskAndInstanceAsync(context, transaction, taskKey, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<CompleteHumanTaskResult>("EW3035_HUMAN_TASK_NOT_FOUND");
            }

            var receiptKey = StoreContractRules.ComputeHumanTaskReceiptKey(command.TaskId, command.IdempotencyKey);
            var existing = await context.HumanTaskReceipts.FindAsync([receiptKey], cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ReceiptMatches(existing, command)
                    ? StoreResults.Idempotent(new CompleteHumanTaskResult(existing.TaskRevision, existing.InstanceRevision, false))
                    : StoreResults.Conflict<CompleteHumanTaskResult>("EW3038_HUMAN_TASK_IDEMPOTENCY_CONFLICT");
            }

            var task = await context.HumanTasks.FindAsync([taskKey], cancellationToken).ConfigureAwait(false)!;
            var instance = await context.Instances.FindAsync([task!.InstanceId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The human task has no instance.");
            if (task.Revision != command.ExpectedTaskRevision || instance.Revision != command.ExpectedInstanceRevision ||
                instance.Status != (int)WorkflowInstanceStatus.Waiting ||
                task.Status is not ((int)HumanTaskStatus.Assigned) and not ((int)HumanTaskStatus.Claimed) ||
                !IdentityEquals(task.AssigneeProviderId, task.AssigneeSubjectId, command.Actor))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<CompleteHumanTaskResult>("EW3039_HUMAN_TASK_REVISION_OR_OWNER_CONFLICT");
            }

            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            task.Status = (int)HumanTaskStatus.Completed;
            task.Revision = checked(task.Revision + 1);
            task.CompletedAtUnixMilliseconds = now;
            task.CompletedActionId = command.ActionId.Value;
            task.CompletedByProviderId = command.Actor.ProviderId.Value;
            task.CompletedBySubjectId = command.Actor.SubjectId;
            var activation = await context.Activations.FindAsync([task.ActivationId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The human task has no activation.");
            activation.Status = (int)NodeExecutionStatus.Succeeded;
            instance.Status = (int)WorkflowInstanceStatus.Running;
            instance.Revision = checked(instance.Revision + 1);
            instance.UpdatedAtUnixMilliseconds = now;
            if (command.ReplacementState is not null)
            {
                instance.StateSchemaVersion = command.ReplacementState.SchemaVersion;
                instance.StateJson = command.ReplacementState.Value.CanonicalText;
            }

            AddNextWork(context, instance.Id, new NextWork(command.NextWork.NodeId, DateTimeOffset.FromUnixTimeMilliseconds(now)), now);
            AddHumanCompletionOutbox(context, command, task, now);
            AddDesignatedAssignments(context, instance.Id, command.DesignatedAssignments);
            context.HumanTaskReceipts.Add(new HumanTaskReceiptRow
            {
                ReceiptKey = receiptKey, TaskId = task.Id, IdempotencyKey = command.IdempotencyKey.Value,
                ActorProviderId = command.Actor.ProviderId.Value, ActorSubjectId = command.Actor.SubjectId,
                ActionId = command.ActionId.Value, RequestSha256 = command.RequestSha256,
                TaskRevision = task.Revision, InstanceRevision = instance.Revision, CommittedAtUnixMilliseconds = now,
            });
            AddAudit(context, instance.Id, "HumanTaskCompleted", instance.Revision, now, command.Actor);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new CompleteHumanTaskResult(task.Revision, instance.Revision, true));
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<StoreResult<OpenHumanTaskPage>> ReadOpenHumanTasksAsync(
        ReadOpenHumanTasksCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Limit is < 1 or > 1000)
            return StoreResults.Conflict<OpenHumanTaskPage>("EW3040_INVALID_HUMAN_TASK_PAGE_LIMIT");
        return await ExecuteAsync(async context =>
        {
            var query = context.HumanTasks.Where(row =>
                row.Status == (int)HumanTaskStatus.AwaitingAssignment || row.Status == (int)HumanTaskStatus.Assigned ||
                row.Status == (int)HumanTaskStatus.Available || row.Status == (int)HumanTaskStatus.Claimed);
            if (command.After is not null)
            {
                var afterId = FormatGuid(command.After.TaskId.Value);
                query = query.Where(row => row.CreatedAtUnixMilliseconds > command.After.CreatedAtUnixMilliseconds ||
                    (row.CreatedAtUnixMilliseconds == command.After.CreatedAtUnixMilliseconds && row.Id.CompareTo(afterId) > 0));
            }

            var rows = await query.OrderBy(row => row.CreatedAtUnixMilliseconds).ThenBy(row => row.Id)
                .Take(command.Limit).ToListAsync(cancellationToken).ConfigureAwait(false);
            var instanceIds = rows.Select(row => row.InstanceId).Distinct(StringComparer.Ordinal).ToArray();
            var instances = await context.Instances.Where(row => instanceIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
            var tasks = rows.Select(row => ToOpenTask(row, instances[row.InstanceId])).ToArray();
            var last = rows.LastOrDefault();
            return StoreResults.Succeeded(new OpenHumanTaskPage(tasks,
                last is null ? null : new HumanTaskCursor(last.CreatedAtUnixMilliseconds, new HumanTaskId(ParseGuid(last.Id)))));
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<StoreResult<FireDueTimerResult>> FireDueTimerAsync(
        FireDueTimerCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var timerKey = FormatGuid(command.TimerId.Value);
            if (!await LockTimerAndInstanceAsync(context, transaction, timerKey, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<FireDueTimerResult>("EW3041_TIMER_NOT_FOUND");
            }

            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var timer = await context.Timers.FindAsync([timerKey], cancellationToken).ConfigureAwait(false)!;
            var instance = await context.Instances.FindAsync([timer!.InstanceId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The timer has no instance.");
            if (timer.Status != (int)WorkflowTimerStatus.Waiting || timer.Revision != command.ExpectedRevision ||
                timer.DueAtUnixMilliseconds > now || instance.Status != (int)WorkflowInstanceStatus.Waiting)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<FireDueTimerResult>("EW3042_TIMER_NOT_DUE_OR_STALE");
            }

            timer.Status = (int)WorkflowTimerStatus.Fired;
            timer.Revision = checked(timer.Revision + 1);
            timer.FiredAtUnixMilliseconds = now;
            var activation = await context.Activations.FindAsync([timer.ActivationId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The timer has no activation.");
            activation.Status = (int)NodeExecutionStatus.Succeeded;
            instance.Status = (int)WorkflowInstanceStatus.Running;
            instance.Revision = checked(instance.Revision + 1);
            instance.UpdatedAtUnixMilliseconds = now;
            AddNextWork(context, instance.Id, new NextWork(new TechnicalId(timer.NextNodeId), DateTimeOffset.FromUnixTimeMilliseconds(now)), now);
            AddAudit(context, instance.Id, "TimerFired", instance.Revision, now);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new FireDueTimerResult(timer.Revision, instance.Revision, new TechnicalId(timer.NextNodeId)));
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<FireNextDueTimerResult>> FireNextDueTimerAsync(
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var timerKey = await LockNextDueTimerAsync(context, transaction, now, cancellationToken).ConfigureAwait(false);
            if (timerKey is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<FireNextDueTimerResult>("EW3043_NO_DUE_TIMER");
            }

            var timer = await context.Timers.FindAsync([timerKey], cancellationToken).ConfigureAwait(false)!;
            var instance = await context.Instances.FindAsync([timer!.InstanceId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The timer has no instance.");
            if (instance.Status != (int)WorkflowInstanceStatus.Waiting)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<FireNextDueTimerResult>("EW3042_TIMER_NOT_DUE_OR_STALE");
            }

            timer.Status = (int)WorkflowTimerStatus.Fired;
            timer.Revision = checked(timer.Revision + 1);
            timer.FiredAtUnixMilliseconds = now;
            var activation = await context.Activations.FindAsync([timer.ActivationId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The timer has no activation.");
            activation.Status = (int)NodeExecutionStatus.Succeeded;
            instance.Status = (int)WorkflowInstanceStatus.Running;
            instance.Revision = checked(instance.Revision + 1);
            instance.UpdatedAtUnixMilliseconds = now;
            AddNextWork(context, instance.Id, new NextWork(new TechnicalId(timer.NextNodeId), DateTimeOffset.FromUnixTimeMilliseconds(now)), now);
            AddAudit(context, instance.Id, "TimerFired", instance.Revision, now);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new FireNextDueTimerResult(
                new WorkflowTimerId(ParseGuid(timer.Id)), new WorkflowInstanceId(ParseGuid(instance.Id)),
                timer.Revision, instance.Revision, new TechnicalId(timer.NextNodeId)));
        }, cancellationToken).ConfigureAwait(false);

    private async ValueTask<StoreResult<HumanTaskMutationResult>> MutateHumanTaskAsync(
        HumanTaskId taskId, long expectedRevision, HumanTaskStatus expectedStatus, HumanTaskStatus newStatus,
        ActorIdentity actor, ActorIdentity? newAssignee, bool requireCurrentAssignee, string eventType,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var key = FormatGuid(taskId.Value);
            if (!await LockHumanTaskAndInstanceAsync(context, transaction, key, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<HumanTaskMutationResult>("EW3035_HUMAN_TASK_NOT_FOUND");
            }

            var task = await context.HumanTasks.FindAsync([key], cancellationToken).ConfigureAwait(false)!;
            var instance = await context.Instances.FindAsync([task!.InstanceId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The human task has no instance.");
            if (task.Revision != expectedRevision || task.Status != (int)expectedStatus ||
                instance.Status != (int)WorkflowInstanceStatus.Waiting ||
                (requireCurrentAssignee && !IdentityEquals(task.AssigneeProviderId, task.AssigneeSubjectId, actor)))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<HumanTaskMutationResult>("EW3039_HUMAN_TASK_REVISION_OR_OWNER_CONFLICT");
            }

            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            task.Status = (int)newStatus;
            task.AssigneeProviderId = newAssignee?.ProviderId.Value;
            task.AssigneeSubjectId = newAssignee?.SubjectId;
            task.Revision = checked(task.Revision + 1);
            instance.Revision = checked(instance.Revision + 1);
            instance.UpdatedAtUnixMilliseconds = now;
            AddAudit(context, instance.Id, eventType, instance.Revision, now, actor);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new HumanTaskMutationResult(newStatus, task.Revision, instance.Revision));
        }, cancellationToken).ConfigureAwait(false);

    private static async Task<bool> LockHumanTaskAndInstanceAsync(
        OracleWorkflowDbContext context, IDbContextTransaction transaction, string taskId, CancellationToken cancellationToken) =>
        await LockWaitAndInstanceAsync(context, transaction,
            "SELECT t.\"ID\" FROM \"EW_HUMAN_TASKS\" t INNER JOIN \"EW_INSTANCES\" i ON i.\"ID\" = t.\"INSTANCE_ID\" WHERE t.\"ID\" = :wait_id FOR UPDATE OF t.\"ID\", i.\"ID\"",
            taskId, cancellationToken).ConfigureAwait(false);

    private static async Task<bool> LockTimerAndInstanceAsync(
        OracleWorkflowDbContext context, IDbContextTransaction transaction, string timerId, CancellationToken cancellationToken) =>
        await LockWaitAndInstanceAsync(context, transaction,
            "SELECT t.\"ID\" FROM \"EW_TIMERS\" t INNER JOIN \"EW_INSTANCES\" i ON i.\"ID\" = t.\"INSTANCE_ID\" WHERE t.\"ID\" = :wait_id FOR UPDATE OF t.\"ID\", i.\"ID\"",
            timerId, cancellationToken).ConfigureAwait(false);

    private static async Task<string?> LockNextDueTimerAsync(
        OracleWorkflowDbContext context, IDbContextTransaction transaction, long now, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT t."ID"
            FROM "EW_TIMERS" t
            INNER JOIN "EW_INSTANCES" i ON i."ID" = t."INSTANCE_ID"
            WHERE t."STATUS" = 0 AND t."DUE_AT_MS" <= :now_ms AND i."STATUS" = 2
            ORDER BY t."DUE_AT_MS", t."CREATED_AT_MS", t."ID"
            FOR UPDATE OF t."ID", i."ID" SKIP LOCKED
            """;
        await using var command = CreateCommand(context, transaction, sql);
        command.Parameters.Add(new OracleParameter("now_ms", OracleDbType.Int64) { Value = now });
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetString(0) : null;
    }

    private static async Task<bool> LockWaitAndInstanceAsync(
        OracleWorkflowDbContext context, IDbContextTransaction transaction, string sql, string id, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(context, transaction, sql);
        command.Parameters.Add(new OracleParameter("wait_id", OracleDbType.Char, 32) { Value = id });
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static HumanTaskSnapshot ToSnapshot(HumanTaskRow task, InstanceRow instance,
        WorkflowDefinition definition, IReadOnlyList<HumanTaskRow> history) => new(
        new HumanTaskId(ParseGuid(task.Id)), new NodeActivationId(ParseGuid(task.ActivationId)),
        new WorkflowInstanceId(ParseGuid(task.InstanceId)), new TechnicalId(task.NodeId),
        (HumanTaskStatus)task.Status, (HumanTaskAssignmentMode)task.AssignmentMode,
        task.AssigneeProviderId is null ? null : new ActorIdentity(new TechnicalId(task.AssigneeProviderId), task.AssigneeSubjectId!),
        task.Revision, instance.Revision,
        new ActorIdentity(new TechnicalId(instance.InitiatorProviderId), instance.InitiatorSubjectId), definition,
        WorkflowState.Create(instance.StateJson, instance.StateSchemaVersion),
        DateTimeOffset.FromUnixTimeMilliseconds(task.CreatedAtUnixMilliseconds),
        history.Select(row => new HumanTaskCompletionActor(new TechnicalId(row.NodeId), new TechnicalId(row.CompletedActionId!),
            new ActorIdentity(new TechnicalId(row.CompletedByProviderId!), row.CompletedBySubjectId!))).ToArray());

    private static OpenHumanTask ToOpenTask(HumanTaskRow task, InstanceRow instance) => new(
        new HumanTaskId(ParseGuid(task.Id)), new WorkflowInstanceId(ParseGuid(task.InstanceId)),
        new TechnicalId(instance.DefinitionId), instance.DefinitionVersion, new TechnicalId(task.NodeId),
        (HumanTaskStatus)task.Status, (HumanTaskAssignmentMode)task.AssignmentMode,
        task.AssigneeProviderId is null ? null : new ActorIdentity(new TechnicalId(task.AssigneeProviderId), task.AssigneeSubjectId!),
        task.Revision, DateTimeOffset.FromUnixTimeMilliseconds(task.CreatedAtUnixMilliseconds));

    private static HumanTaskCompletionReceipt ToReceipt(HumanTaskReceiptRow row) => new(
        new HumanTaskId(ParseGuid(row.TaskId)), new TechnicalId(row.IdempotencyKey),
        new ActorIdentity(new TechnicalId(row.ActorProviderId), row.ActorSubjectId),
        new TechnicalId(row.ActionId), row.RequestSha256, row.TaskRevision, row.InstanceRevision);

    private static bool ReceiptMatches(HumanTaskReceiptRow row, CompleteHumanTaskCommand command) =>
        string.Equals(row.TaskId, FormatGuid(command.TaskId.Value), StringComparison.Ordinal) &&
        string.Equals(row.IdempotencyKey, command.IdempotencyKey.Value, StringComparison.Ordinal) &&
        IdentityEquals(row.ActorProviderId, row.ActorSubjectId, command.Actor) &&
        string.Equals(row.ActionId, command.ActionId.Value, StringComparison.Ordinal) &&
        string.Equals(row.RequestSha256, command.RequestSha256, StringComparison.Ordinal);

    private static bool IdentityEquals(string? providerId, string? subjectId, ActorIdentity actor) =>
        string.Equals(providerId, actor.ProviderId.Value, StringComparison.Ordinal) &&
        string.Equals(subjectId, actor.SubjectId, StringComparison.Ordinal);

    private static void AddHumanCompletionOutbox(
        OracleWorkflowDbContext context, CompleteHumanTaskCommand command, HumanTaskRow task, long now)
    {
        foreach (var write in command.Outbox ?? [])
        {
            context.Outbox.Add(new OutboxRow
            {
                Id = FormatGuid(Guid.NewGuid()), InstanceId = task.InstanceId, ActivationId = task.ActivationId,
                OperationId = write.OperationId.Value, Destination = write.Destination.Value,
                ContentType = write.ContentType, PayloadJson = write.PayloadJson,
                IdempotencyKey = ComputeOutboxIdempotencyKey(task.InstanceId, task.ActivationId, write.OperationId.Value),
                Status = (int)OutboxMessageStatus.Ready, Attempt = 0, DueAtUnixMilliseconds = now,
                Generation = 0, CreatedAtUnixMilliseconds = now,
            });
        }
    }
}

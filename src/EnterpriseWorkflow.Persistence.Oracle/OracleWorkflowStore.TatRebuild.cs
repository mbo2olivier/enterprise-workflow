using EnterpriseWorkflow.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Oracle;

public sealed partial class OracleWorkflowStore
{
    /// <summary>Reconstructs one instance's reporting rows from its retained TAT event times.</summary>
    public async ValueTask<StoreResult<bool>> RebuildTatAsync(WorkflowInstanceId instanceId, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var id = FormatGuid(instanceId.Value);
            var source = await context.Instances.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false);
            if (source is null) return StoreResults.NotFound<bool>("store.instance-unavailable");
            var events = await context.Set<TatEventRow>().AsNoTracking().Where(row => row.InstanceId == id).OrderBy(row => row.AtMs).ThenBy(row => row.SourceRevision).Take(50001).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (events.Length == 0 || events.Length > 50000) return StoreResults.Conflict<bool>("store.tat-rebuild-unavailable");
            var root = await context.Set<TatInstanceRow>().FindAsync([id], cancellationToken).ConfigureAwait(false);
            if (root is null) return StoreResults.Conflict<bool>("store.tat-root-missing");
            root.StartedAtMs = source.CreatedAtUnixMilliseconds;
            root.EndedAtMs = IsTerminal((WorkflowInstanceStatus)source.Status) ? source.UpdatedAtUnixMilliseconds : null;
            root.Status = source.Status;
            root.SourceRevision = source.Revision;
            root.Coverage = events.Any(item => item.SourceKind == "instance" && item.EventType == "InstanceStarted") ? 1 : 0;
            await context.Set<TatAssignmentRow>().Where(row => row.InstanceId == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await context.Set<TatStepRow>().Where(row => row.InstanceId == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            var activations = await context.Activations.AsNoTracking().Where(row => row.InstanceId == id).ToDictionaryAsync(row => row.Id, cancellationToken).ConfigureAwait(false);
            var tasks = await context.HumanTasks.AsNoTracking().Where(row => row.InstanceId == id).ToDictionaryAsync(row => row.Id, cancellationToken).ConfigureAwait(false);
            foreach (var group in events.Where(item => item.SourceKind == "step").GroupBy(item => item.SourceId))
            {
                var ordered = group.OrderBy(item => item.AtMs).ThenBy(item => item.SourceRevision).ToArray();
                var first = ordered[0];
                var last = ordered[^1];
                if (!activations.TryGetValue(group.Key, out var activation)) return StoreResults.Conflict<bool>("store.tat-source-missing");
                var terminal = last.Status is (int)NodeExecutionStatus.Succeeded or (int)NodeExecutionStatus.Failed or (int)NodeExecutionStatus.Cancelled;
                context.Add(new TatStepRow { Id = group.Key, InstanceId = id, NodeId = activation.NodeId, StartedAtMs = first.AtMs,
                    EndedAtMs = terminal ? last.AtMs : null, Status = last.Status, Attempt = activation.Attempt, Coverage = root.Coverage });
            }
            foreach (var group in events.Where(item => item.SourceKind == "task").GroupBy(item => item.SourceId))
            {
                if (!tasks.TryGetValue(group.Key, out var task)) return StoreResults.Conflict<bool>("store.tat-source-missing");
                var ordered = group.OrderBy(item => item.SourceRevision).ToArray();
                for (var index = 0; index < ordered.Length; index++)
                {
                    var current = ordered[index];
                    if (current.Status is (int)HumanTaskStatus.Completed or (int)HumanTaskStatus.Cancelled) continue;
                    var next = index + 1 < ordered.Length ? ordered[index + 1] : null;
                    context.Add(new TatAssignmentRow { Id = task.Id + ":" + current.SourceRevision, InstanceId = id, StepId = task.ActivationId, TaskId = task.Id,
                        ActorProviderId = current.ActorProviderId, ActorSubjectId = current.ActorSubjectId, StartedAtMs = current.AtMs,
                        EndedAtMs = next?.AtMs ?? root.EndedAtMs, EndReason = next?.EventType, SourceRevision = current.SourceRevision,
                        Coverage = ordered[0].SourceRevision == 0 ? root.Coverage : 0 });
                }
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(true);
        }, cancellationToken).ConfigureAwait(false);
}

using System.Security.Cryptography;
using System.Text;
using EnterpriseWorkflow.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Sqlite;

public sealed partial class SqliteWorkflowStore
{
    // Invoked inside the existing source transaction before SaveChanges. No best-effort writes.
    private static async Task ProjectTatAsync(SqliteWorkflowDbContext context, CancellationToken token)
    {
        context.ChangeTracker.DetectChanges();
        var audits = context.ChangeTracker.Entries<AuditRow>().Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToArray();
        foreach (var audit in audits)
        {
            var instance = context.ChangeTracker.Entries<InstanceRow>().Select(entry => entry.Entity).SingleOrDefault(row => row.Id == audit.InstanceId);
            if (instance is null) continue;
            var projection = await context.Set<TatInstanceRow>().FindAsync([instance.Id], token).ConfigureAwait(false);
            if (projection is null)
            {
                projection = new() { Id = instance.Id, ProcessId = instance.DefinitionId, ProcessVersion = instance.DefinitionVersion,
                    InitiatorProviderId = instance.InitiatorProviderId, InitiatorSubjectId = instance.InitiatorSubjectId,
                    StartedAtMs = instance.CreatedAtUnixMilliseconds, Coverage = audit.EventType == "InstanceStarted" ? 1 : 0 };
                context.Add(projection);
            }
            projection.Status = instance.Status;
            projection.SourceRevision = instance.Revision;
            if (IsTerminal((WorkflowInstanceStatus)instance.Status)) projection.EndedAtMs = audit.OccurredAtUnixMilliseconds;
            AddEvent("instance", instance.Id, instance.Status, instance.Revision, audit.ActorProviderId, audit.ActorSubjectId);

            foreach (var entry in context.ChangeTracker.Entries<ActivationRow>().Where(entry => entry.Entity.InstanceId == instance.Id && entry.State is EntityState.Added or EntityState.Modified).ToArray())
            {
                var activation = entry.Entity;
                var step = await context.Set<TatStepRow>().FindAsync([activation.Id], token).ConfigureAwait(false);
                if (step is null)
                {
                    step = new() { Id = activation.Id, InstanceId = instance.Id, NodeId = activation.NodeId,
                        StartedAtMs = audit.OccurredAtUnixMilliseconds, Coverage = entry.State == EntityState.Added && projection.Coverage == 1 ? 1 : 0 };
                    context.Add(step);
                }
                step.Status = activation.Status;
                step.Attempt = activation.Attempt;
                if (activation.Status is (int)NodeExecutionStatus.Succeeded or (int)NodeExecutionStatus.Failed or (int)NodeExecutionStatus.Cancelled)
                    step.EndedAtMs = audit.OccurredAtUnixMilliseconds;
                AddEvent("step", activation.Id, activation.Status, instance.Revision, null, null);
            }
            foreach (var entry in context.ChangeTracker.Entries<HumanTaskRow>().Where(entry => entry.Entity.InstanceId == instance.Id && entry.State is EntityState.Added or EntityState.Modified).ToArray())
            {
                var task = entry.Entity;
                var intervals = await context.Set<TatAssignmentRow>().Where(row => row.TaskId == task.Id && row.EndedAtMs == null).ToListAsync(token).ConfigureAwait(false);
                foreach (var interval in intervals) { interval.EndedAtMs = audit.OccurredAtUnixMilliseconds; interval.EndReason = audit.EventType; }
                if (task.Status is not ((int)HumanTaskStatus.Completed) and not ((int)HumanTaskStatus.Cancelled))
                {
                    context.Add(new TatAssignmentRow { Id = task.Id + ":" + task.Revision, InstanceId = instance.Id, StepId = task.ActivationId, TaskId = task.Id,
                        ActorProviderId = task.AssigneeProviderId, ActorSubjectId = task.AssigneeSubjectId, StartedAtMs = audit.OccurredAtUnixMilliseconds,
                        SourceRevision = task.Revision, Coverage = projection.Coverage == 1 && (entry.State == EntityState.Added || intervals.Count > 0) ? 1 : 0 });
                }
                AddEvent("task", task.Id, task.Status, task.Revision, task.AssigneeProviderId, task.AssigneeSubjectId);
            }
            if (projection.EndedAtMs is not null)
            {
                var open = await context.Set<TatAssignmentRow>().Where(row => row.InstanceId == instance.Id && row.EndedAtMs == null).ToListAsync(token).ConfigureAwait(false);
                foreach (var interval in open) { interval.EndedAtMs = projection.EndedAtMs; interval.EndReason = audit.EventType; }
            }

            void AddEvent(string kind, string sourceId, int status, long revision, string? provider, string? subject)
            {
                var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{instance.Id}:{instance.Revision}:{kind}:{sourceId}:{audit.EventType}")));
                context.Add(new TatEventRow { Id = id, InstanceId = instance.Id, SourceId = sourceId, SourceKind = kind,
                    EventType = audit.EventType, AtMs = audit.OccurredAtUnixMilliseconds, SourceRevision = revision, Status = status,
                    ActorProviderId = provider, ActorSubjectId = subject });
            }
        }
    }
}

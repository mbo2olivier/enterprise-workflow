using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Oracle;

public sealed partial class OracleWorkflowStore
{
    public async ValueTask<StoreResult<IReadOnlyList<WorkflowTatRow>>> ReadTatAsync(WorkflowTatQuery query, CancellationToken cancellationToken)
    {
        if (query.ProcessVersion is <= 0 || query.CompletedFromUtc is { } from && query.CompletedUntilUtc is { } until && from >= until)
            return StoreResults.Conflict<IReadOnlyList<WorkflowTatRow>>("store.tat-query-invalid");
        return await ExecuteAsync(async context =>
        {
            IQueryable<TatObservation> observations;
            if (query.ByAnalyst || query.Analyst is not null)
            {
                observations = from interval in context.Set<TatAssignmentRow>().AsNoTracking()
                    join step in context.Set<TatStepRow>() on interval.StepId equals step.Id
                    join instance in context.Set<TatInstanceRow>() on interval.InstanceId equals instance.Id
                    join task in context.HumanTasks on interval.TaskId equals task.Id
                    where interval.Coverage == 1 && step.Coverage == 1 && instance.Coverage == 1 && interval.ActorProviderId != null && (interval.EndedAtMs == null || task.CompletedAtUnixMilliseconds != null)
                    select new TatObservation { UnitId = task.Id, CompletionAt = task.CompletedAtUnixMilliseconds, ProcessId = instance.ProcessId, Version = instance.ProcessVersion, NodeId = step.NodeId,
                        ProviderId = interval.ActorProviderId, SubjectId = interval.ActorSubjectId, Start = interval.StartedAtMs, End = interval.EndedAtMs };
            }
            else
            {
                observations = from step in context.Set<TatStepRow>().AsNoTracking()
                    join instance in context.Set<TatInstanceRow>() on step.InstanceId equals instance.Id
                    where step.Coverage == 1 && instance.Coverage == 1
                    select new TatObservation { UnitId = step.Id, CompletionAt = step.EndedAtMs, ProcessId = instance.ProcessId, Version = instance.ProcessVersion, NodeId = step.NodeId,
                        Start = step.StartedAtMs, End = step.EndedAtMs };
            }
            if (!string.IsNullOrEmpty(query.ProcessId)) observations = observations.Where(row => row.ProcessId == query.ProcessId);
            if (query.ProcessVersion is { } version) observations = observations.Where(row => row.Version == version);
            if (!string.IsNullOrEmpty(query.NodeId)) observations = observations.Where(row => row.NodeId == query.NodeId);
            if (query.Analyst is { } actor) observations = observations.Where(row => row.ProviderId == actor.ProviderId.Value && row.SubjectId == actor.SubjectId);
            if (query.CompletedFromUtc is { } start) { var ms = start.ToUnixTimeMilliseconds(); observations = observations.Where(row => row.End == null || row.CompletionAt >= ms); }
            if (query.CompletedUntilUtc is { } end) { var ms = end.ToUnixTimeMilliseconds(); observations = observations.Where(row => row.End == null || row.CompletionAt < ms); }
            var rows = await observations.Take(10001).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (rows.Length > 10000) return StoreResults.Conflict<IReadOnlyList<WorkflowTatRow>>("store.tat-report-limit");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            IReadOnlyList<WorkflowTatRow> report = rows.GroupBy(row => (row.ProcessId, row.Version, row.NodeId, row.ProviderId, row.SubjectId))
                .OrderBy(group => group.Key.ProcessId, StringComparer.Ordinal).ThenBy(group => group.Key.Version).ThenBy(group => group.Key.NodeId, StringComparer.Ordinal)
                .Select(group =>
                {
                    var durations = group.Where(row => row.End != null && row.CompletionAt != null).GroupBy(row => row.UnitId)
                        .Select(unit => unit.Sum(row => Math.Max(0, row.End!.Value - row.Start))).ToArray();
                    var open = group.Where(row => row.End == null).Select(row => Math.Max(0, now - row.Start)).ToArray();
                    return new WorkflowTatRow(group.Key.ProcessId, group.Key.Version, group.Key.NodeId,
                        group.Key.ProviderId is null ? null : new(new(group.Key.ProviderId), group.Key.SubjectId!),
                        durations.Length, durations.Length == 0 ? null : durations.Average(), durations.Length == 0 ? null : durations.Min(),
                        durations.Length == 0 ? null : durations.Max(), open.Length, open.Length == 0 ? null : open.Max());
                }).ToArray();
            return StoreResults.Succeeded(report);
        }, cancellationToken).ConfigureAwait(false);
    }
    private sealed class TatObservation
    {
        public required string UnitId { get; init; }
        public long? CompletionAt { get; init; }
        public required string ProcessId { get; init; }
        public int Version { get; init; }
        public required string NodeId { get; init; }
        public string? ProviderId { get; init; }
        public string? SubjectId { get; init; }
        public long Start { get; init; }
        public long? End { get; init; }
    }
}

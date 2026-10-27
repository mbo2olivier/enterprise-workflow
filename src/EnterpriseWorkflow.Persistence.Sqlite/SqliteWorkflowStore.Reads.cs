// String comparison is translated to SQL; the ordinal overload is not provider-translatable.
#pragma warning disable CA1309
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Serialization;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Sqlite;

public sealed partial class SqliteWorkflowStore
{
    public async ValueTask<StoreResult<IReadOnlyList<WorkflowInstanceCandidate>>> SearchInstancesAsync(WorkflowInstanceSearch query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > 200 || query.ProcessVersion is <= 0 || query.BusinessKey?.Length > 256 ||
            (query.CreatedFromUtc is { } from && query.CreatedUntilUtc is { } until && from >= until))
            return StoreResults.Conflict<IReadOnlyList<WorkflowInstanceCandidate>>("store.invalid-instance-search");
        return await ExecuteAsync(async context =>
        {
            var rows = context.Instances.AsNoTracking().AsQueryable();
            if (query.Statuses is { Length: > 0 } statuses) { var values = statuses.Select(item => (int)item).ToArray(); rows = rows.Where(row => values.Contains(row.Status)); }
            if (query.Status is { } status) rows = rows.Where(row => row.Status == (int)status);
            if (query.CreatedFromUtc is { } start) { var ms = start.ToUnixTimeMilliseconds(); rows = rows.Where(row => row.CreatedAtUnixMilliseconds >= ms); }
            if (query.CreatedUntilUtc is { } end) { var ms = end.ToUnixTimeMilliseconds(); rows = rows.Where(row => row.CreatedAtUnixMilliseconds < ms); }
            if (query.ProcessId is { } process) rows = rows.Where(row => row.DefinitionId == process);
            if (query.ProcessVersion is { } version) rows = rows.Where(row => row.DefinitionVersion == version);
            if (query.InstanceId is { } id) { var key = FormatGuid(id.Value); rows = rows.Where(row => row.Id == key); }
            if (query.PublicReference is { } reference) rows = rows.Where(row => row.PublicReference == reference);
            if (query.BusinessKey is { } business) rows = rows.Where(row => row.BusinessKey == business);
            if (query.Initiator is { } actor) rows = rows.Where(row => row.InitiatorProviderId == actor.ProviderId.Value && row.InitiatorSubjectId == actor.SubjectId);
            if (query.After is { } cursor) rows = rows.Where(row => row.CreatedAtUnixMilliseconds < cursor.CreatedAtUnixMilliseconds ||
                (row.CreatedAtUnixMilliseconds == cursor.CreatedAtUnixMilliseconds && string.Compare(row.Id, cursor.InstanceId) < 0));
            var page = await rows.OrderByDescending(row => row.CreatedAtUnixMilliseconds).ThenByDescending(row => row.Id).Take(query.Limit).ToListAsync(cancellationToken).ConfigureAwait(false);
            var summaries = await SummariesAsync(context, page, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<WorkflowInstanceCandidate> items = summaries.Select(summary => new WorkflowInstanceCandidate(summary, summary.Initiator!)).ToArray();
            return StoreResults.Succeeded(items);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<StoreResult<WorkflowInstanceReadSnapshot>> ReadInstanceAsync(WorkflowInstanceId instanceId, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var row = await context.Instances.AsNoTracking().SingleOrDefaultAsync(row => row.Id == FormatGuid(instanceId.Value), cancellationToken).ConfigureAwait(false);
            if (row is null) return StoreResults.NotFound<WorkflowInstanceReadSnapshot>("store.instance-unavailable");
            var definition = await context.Definitions.AsNoTracking().SingleAsync(item => item.DefinitionId == row.DefinitionId && item.Version == row.DefinitionVersion, cancellationToken).ConfigureAwait(false);
            var model = PublishedWorkflowDefinitionReader.Read(definition.CanonicalJson, definition.DefinitionId, definition.Version, definition.Sha256);
            var activations = await context.Activations.AsNoTracking().Where(item => item.InstanceId == row.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            var audits = await context.Audits.AsNoTracking().Where(item => item.InstanceId == row.Id).OrderByDescending(item => item.Sequence).Take(100).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new WorkflowInstanceReadSnapshot(Summary(row, activations.Select(item => new WorkflowInstancePosition(item.NodeId, (NodeExecutionStatus)item.Status)).Distinct().ToArray()), model,
                new(new(row.InitiatorProviderId), row.InitiatorSubjectId), WorkflowState.Create(row.StateJson, row.StateSchemaVersion),
                activations.Select(item => new WorkflowInstanceStep(item.NodeId, model.Nodes.Single(node => node.Id.Value == item.NodeId).DisplayLabel ?? item.NodeId, (NodeExecutionStatus)item.Status, item.Attempt)).ToArray(),
                audits.Select(item => new WorkflowInstanceHistoryEntry(item.Sequence, item.EventType, DateTimeOffset.FromUnixTimeMilliseconds(item.OccurredAtUnixMilliseconds),
                    item.ActorProviderId is null ? null : new(new(item.ActorProviderId), item.ActorSubjectId!))).ToArray()));
        }, cancellationToken).ConfigureAwait(false);

    private static WorkflowInstanceSummary Summary(InstanceRow row, IReadOnlyList<WorkflowInstancePosition>? positions = null) => new(new(ParseGuid(row.Id)), new(row.DefinitionId), row.DefinitionVersion,
        (WorkflowInstanceStatus)row.Status, row.Revision, row.BusinessKey, DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAtUnixMilliseconds), DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAtUnixMilliseconds), row.PublicReference, new(new(row.InitiatorProviderId), row.InitiatorSubjectId), positions);
    private static async Task<IReadOnlyList<WorkflowInstanceSummary>> SummariesAsync(SqliteWorkflowDbContext context, IReadOnlyList<InstanceRow> rows, CancellationToken token)
    {
        var ids = rows.Select(row => row.Id).ToArray();
        var positions = await context.Activations.AsNoTracking().Where(row => ids.Contains(row.InstanceId))
            .Select(row => new { row.InstanceId, row.NodeId, row.Status }).Distinct().ToArrayAsync(token).ConfigureAwait(false);
        var grouped = positions.ToLookup(row => row.InstanceId, StringComparer.Ordinal);
        return rows.Select(row => Summary(row, grouped[row.Id].Select(position => new WorkflowInstancePosition(position.NodeId, (NodeExecutionStatus)position.Status)).ToArray())).ToArray();
    }

}

using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Serialization;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseWorkflow.Persistence.Oracle;
public sealed partial class OracleWorkflowStore
{
    public async ValueTask<StoreResult<IReadOnlyList<WorkflowAuditRow>>> SearchAuditAsync(WorkflowAuditQuery query, CancellationToken cancellationToken)
    {
        if (query.Limit is < 1 or > 10001) return StoreResults.Conflict<IReadOnlyList<WorkflowAuditRow>>("store.invalid-audit-query");
        return await ExecuteAsync(async context =>
        {
            var rows = from audit in context.Audits.AsNoTracking()
                       join instance in context.Instances.AsNoTracking() on audit.InstanceId equals instance.Id
                       join definition in context.Definitions.AsNoTracking() on new { instance.DefinitionId, Version = instance.DefinitionVersion } equals new { definition.DefinitionId, definition.Version }
                       select new { Audit = audit, Instance = instance, Definition = definition };
            if (query.FromUtc is { } from) { var ms = from.ToUnixTimeMilliseconds(); rows = rows.Where(row => row.Audit.OccurredAtUnixMilliseconds >= ms); }
            if (query.UntilUtc is { } until) { var ms = until.ToUnixTimeMilliseconds(); rows = rows.Where(row => row.Audit.OccurredAtUnixMilliseconds < ms); }
            if (query.ActorId is { } actor) rows = actor == "system" ? rows.Where(row => row.Audit.ActorSubjectId == null) : rows.Where(row => row.Audit.ActorSubjectId == actor);
            if (query.ActorProvider is { } provider) rows = provider == "system" ? rows.Where(row => row.Audit.ActorProviderId == null) : rows.Where(row => row.Audit.ActorProviderId == provider);
            if (query.Action is { } action) rows = rows.Where(row => row.Audit.ActionId == action || row.Audit.EventType == action);
            if (query.Outcome is { } outcome) rows = rows.Where(row => row.Audit.Outcome == outcome);
            if (query.ProcessId is { } process) rows = rows.Where(row => row.Instance.DefinitionId == process);
            if (query.ProcessIds is { } processes) rows = rows.Where(row => processes.Contains(row.Instance.DefinitionId));
            if (query.Stage is { } stage) rows = rows.Where(row => row.Audit.NodeId == stage);
            if (query.Target is { } target) rows = rows.Where(row => row.Instance.PublicReference == target || row.Instance.Id == target);
            if (query.BeforeTime is { } before)
                rows = rows.Where(row => row.Audit.OccurredAtUnixMilliseconds < before || (row.Audit.OccurredAtUnixMilliseconds == before && row.Audit.Sequence < query.BeforeSequence));
            var found = await rows.OrderByDescending(row => row.Audit.OccurredAtUnixMilliseconds).ThenByDescending(row => row.Audit.Sequence).Take(query.Limit).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            var definitions = new Dictionary<(string Id, int Version), WorkflowDefinition>();
            IReadOnlyList<WorkflowAuditRow> result = found.Select(row =>
            {
                var key = (row.Definition.DefinitionId, row.Definition.Version);
                if (!definitions.TryGetValue(key, out var model))
                {
                    model = PublishedWorkflowDefinitionReader.Read(row.Definition.CanonicalJson, row.Definition.DefinitionId, row.Definition.Version, row.Definition.Sha256);
                    definitions.Add(key, model);
                }
                var module = WorkflowStagePolicy.Read(model.Nodes.Single(node => node.Role == WorkflowNodeRole.Start).Configuration)?.MediaPolicyModule ?? model.Artifacts.FirstOrDefault()?.Id.Value;
                var stageNode = model.Nodes.FirstOrDefault(node => node.Id.Value == row.Audit.NodeId);
                var actionLabel = stageNode is null ? null : WorkflowStagePolicy.Read(stageNode.Configuration)?.Actions.GetValueOrDefault(row.Audit.ActionId ?? string.Empty)?.Label;
                return new WorkflowAuditRow(row.Audit.Sequence, DateTimeOffset.FromUnixTimeMilliseconds(row.Audit.OccurredAtUnixMilliseconds),
                    row.Audit.ActorProviderId is null ? null : new ActorIdentity(new(row.Audit.ActorProviderId), row.Audit.ActorSubjectId!) { DisplayName = row.Audit.ActorDisplayName },
                    row.Audit.ActionId ?? row.Audit.EventType, row.Audit.EventType, row.Audit.Outcome, module, model.Id.Value, model.DisplayLabel ?? model.Id.Value,
                    row.Audit.NodeId, model.Nodes.FirstOrDefault(node => node.Id.Value == row.Audit.NodeId)?.DisplayLabel, row.Instance.PublicReference, actionLabel);
            }).ToArray();
            return StoreResults.Succeeded(result);
        }, cancellationToken).ConfigureAwait(false);
    }
}

using System.Text;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Presentation;

public sealed class WorkflowTatService(IWorkflowStore store, IAuthorizationProvider authorization, ISecurityProfileStore profiles)
{
    public async ValueTask<StoreResult<IReadOnlyList<WorkflowTatRow>>> ReadAsync(IdentityReference actor, WorkflowTatQuery query, CancellationToken token)
    {
        if (!(await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ReadTat), token)).Allowed)
            return StoreResults.Conflict<IReadOnlyList<WorkflowTatRow>>("security.tat-denied");
        return await store.ReadTatAsync(query, token);
    }

    public async ValueTask<WorkflowAuditExport> ExportAsync(IdentityReference actor, WorkflowTatQuery query, CancellationToken token)
    {
        if (!(await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ExportTat), token)).Allowed) return new(null, "security.tat-denied");
        var result = await ReadAsync(actor, query, token);
        if (result.Outcome != StoreOutcome.Succeeded || result.Value is null) return new(null, result.ErrorCode);
        var csv = new StringBuilder("Processus,Version,Étape,Fournisseur,Analyste,Observations,Moyenne ms,Minimum ms,Maximum ms,Ouvertes,Âge maximum ms\r\n");
        foreach (var row in result.Value)
            csv.AppendLine(string.Join(',', new[] { row.ProcessId, row.ProcessVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), row.NodeId,
                row.Analyst?.ProviderId.Value ?? "", row.Analyst?.SubjectId ?? "", Format(row.CompletedCount), Format(row.MeanMilliseconds), Format(row.MinimumMilliseconds),
                Format(row.MaximumMilliseconds), Format(row.OpenCount), Format(row.OldestOpenMilliseconds) }.Select(WorkflowAuditService.Csv)));
        if (!(await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ReadTat), token)).Allowed ||
            !(await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ExportTat), token)).Allowed) return new(null, "security.tat-denied");
        var audit = await profiles.RecordAuditAsync(new(DateTimeOffset.UtcNow, actor, "workflow.tat.export", $"rows:{result.Value.Count}", "succeeded"), token);
        return audit.Outcome == ProviderOutcome.Succeeded ? new(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), null) : new(null, "security.tat-unavailable");
    }
    private static string Format(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
}

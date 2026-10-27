using System.Text;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Core.Model;
using System.Text.Json;
using EnterpriseWorkflow.Security;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Presentation;

public sealed class WorkflowAuditOptions
{
    public int MaximumExportRows { get; set; } = 10000;
    public int MaximumExportBytes { get; set; } = 16 * 1024 * 1024;
    public TimeSpan ExportTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public void Validate()
    {
        if (MaximumExportRows is < 1 or > 10000 || MaximumExportBytes is < 1024 or > 64 * 1024 * 1024 || ExportTimeout <= TimeSpan.Zero || ExportTimeout > TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("Invalid audit export limits.");
    }
}
public sealed record WorkflowAuditExport(byte[]? Content, string? ErrorCode);

public sealed class WorkflowAuditService(ISecurityProfileStore profiles, IAuthorizationProvider authorization, IOptions<WorkflowAuditOptions> configured, IWorkflowStore? workflows = null, IWorkflowPresentationCatalog? catalog = null, IIdentityDisplayNameResolver? names = null)
{
    public async ValueTask<ProviderResult<IReadOnlyList<SecurityAuditRecord>>> SearchAsync(IdentityReference actor, SecurityAuditQuery query, CancellationToken cancellationToken)
    {
        if (!(await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ReadAudit), cancellationToken)).Allowed)
            return new(ProviderOutcome.Forbidden);
        return await SearchCoreAsync(query with { Limit = Math.Clamp(query.Limit, 1, 100) }, cancellationToken);
    }

    public async ValueTask<WorkflowAuditExport> ExportAsync(IdentityReference actor, SecurityAuditQuery query, CancellationToken cancellationToken)
    {
        var options = configured.Value;
        options.Validate();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.ExportTimeout);
        var token = timeout.Token;
        if (!(await Allowed(actor, token))) return new(null, "security.audit-export-denied");
        // Each source is bounded; merge before exporting and refuse rather than truncate.
        var result = await SearchCoreAsync(query with { Limit = options.MaximumExportRows + 1, After = null }, token);
        if (result.Outcome != ProviderOutcome.Succeeded || result.Value is null) return new(null, "security.audit-export-unavailable");
        if (result.Value.Count > options.MaximumExportRows) return new(null, "security.audit-export-limit");
        using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        {
            await writer.WriteLineAsync("Date UTC,Fournisseur,Acteur,Nom de l’acteur,Module,Processus,Étape,Action,Cible,Résultat");
            foreach (var row in result.Value)
            {
                token.ThrowIfCancellationRequested();
                var entry = row.Entry;
                await writer.WriteLineAsync(string.Join(',', new[] { entry.OccurredAtUtc.ToString("O"), entry.Actor.ProviderId, entry.Actor.SubjectId, row.ActorDisplayName ?? entry.Actor.SubjectId, row.Module ?? "", row.ProcessTitle ?? row.Process ?? "", row.StageLabel ?? row.Stage ?? "", row.ActionLabel is null ? entry.Action : $"{row.ActionLabel} ({entry.Action})", entry.Target, entry.Outcome }.Select(Csv)));
                await writer.FlushAsync(token);
                if (stream.Length > options.MaximumExportBytes) return new(null, "security.audit-export-limit");
            }
        }
        if (!(await Allowed(actor, token))) return new(null, "security.audit-export-denied");
        var recorded = await profiles.RecordAuditAsync(new(DateTimeOffset.UtcNow, actor, "security.audit.export",
            $"rows:{result.Value.Count};filters:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(query with { After = null, Limit = 0 })))}", "succeeded"), token);
        return recorded.Outcome == ProviderOutcome.Succeeded ? new(stream.ToArray(), null) : new(null, "security.audit-export-unavailable");
    }

    private async ValueTask<ProviderResult<IReadOnlyList<SecurityAuditRecord>>> SearchCoreAsync(SecurityAuditQuery query, CancellationToken token)
    {
        if (query.Limit is < 1 or > 10001 || query.FromUtc >= query.UntilUtc || query.ActorId?.Length > 512 ||
            query.Action?.Length > 160 || query.Module?.Length > 128 || query.Process?.Length > 128 || query.Stage?.Length > 128)
            return new(ProviderOutcome.Conflict);
        var securityCursor = query.After;
        var workflowCursor = query.After;
        var securityUntil = query.UntilUtc;
        var workflowUntil = query.UntilUtc;
        long beforeSequence = long.MaxValue;
        if (query.After is { } cursor)
        {
            if (cursor.Id.StartsWith("w:", StringComparison.Ordinal))
            {
                if (!long.TryParse(cursor.Id.AsSpan(2), out beforeSequence)) return new(ProviderOutcome.Conflict);
                securityCursor = null;
                var ceiling = DateTimeOffset.FromUnixTimeMilliseconds(cursor.OccurredAtUnixMilliseconds + 1);
                securityUntil = securityUntil is { } end && end < ceiling ? end : ceiling;
            }
            else
            {
                var id = cursor.Id.StartsWith("s:", StringComparison.Ordinal) ? cursor.Id[2..] : cursor.Id;
                if (!Guid.TryParseExact(id, "N", out _)) return new(ProviderOutcome.Conflict);
                securityCursor = cursor with { Id = id };
                workflowCursor = null;
                var ceiling = DateTimeOffset.FromUnixTimeMilliseconds(cursor.OccurredAtUnixMilliseconds);
                workflowUntil = workflowUntil is { } end && end < ceiling ? end : ceiling;
            }
        }
        var combined = new List<SecurityAuditRecord>();
        if (query.Module is null && query.Process is null && query.Stage is null)
        {
            var security = await profiles.SearchAuditAsync(query with { After = securityCursor, UntilUtc = securityUntil }, token).ConfigureAwait(false);
            if (security.Outcome != ProviderOutcome.Succeeded || security.Value is null) return security;
            foreach (var row in security.Value)
                combined.Add(row with { Id = "s:" + row.Id, ActorDisplayName = names is null ? row.Entry.Actor.SubjectId : await names.ResolveAsync(row.Entry.Actor, token).ConfigureAwait(false) });
        }
        if (workflows is not null)
        {
            var processIds = query.Module is null ? null : catalog?.Processes.Where(process => process.Definition.Artifacts.Any(artifact => artifact.Id.Value == query.Module))
                .Select(process => process.Definition.Id.Value).Distinct(StringComparer.Ordinal).ToArray() ?? [];
            var business = await workflows.SearchAuditAsync(new(query.FromUtc, workflowUntil, query.Actor?.SubjectId ?? query.ActorId, query.Actor?.ProviderId ?? query.ActorProvider,
                query.Action, query.Outcome, query.Process, processIds, query.Stage, query.Limit, workflowCursor?.OccurredAtUnixMilliseconds, beforeSequence, query.Target), token).ConfigureAwait(false);
            if (business.Outcome != StoreOutcome.Succeeded || business.Value is null) return new(ProviderOutcome.Unavailable);
            foreach (var row in business.Value)
            {
                var actor = row.Actor is { } identity ? new IdentityReference(identity.ProviderId.Value, identity.SubjectId) : new IdentityReference("system", "system");
                var name = row.Actor is null ? "Système" : row.Actor.DisplayName ?? (names is null ? actor.SubjectId : await names.ResolveAsync(actor, token).ConfigureAwait(false));
                combined.Add(new("w:" + row.Sequence.ToString("D20", System.Globalization.CultureInfo.InvariantCulture), new(row.Date, actor, row.Action, row.Reference, row.Outcome),
                    row.Module, row.ProcessId, row.ProcessTitle, row.StageId, row.StageLabel, name, row.ActionLabel));
            }
        }
        return new(ProviderOutcome.Succeeded, combined.OrderByDescending(row => row.Entry.OccurredAtUtc).ThenByDescending(row => row.Id, StringComparer.Ordinal).Take(query.Limit).ToArray());
    }

    private async ValueTask<bool> Allowed(IdentityReference actor, CancellationToken token) =>
        (await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ReadAudit), token)).Allowed &&
        (await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ExportAudit), token)).Allowed;

    public static string Csv(string value)
    {
        var first = value.AsSpan().TrimStart();
        if (!first.IsEmpty && (first[0] is '=' or '+' or '-' or '@' || value[0] is '\t' or '\r' or '\n')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

// String comparison is translated to SQL; the ordinal overload is not provider-translatable.
#pragma warning disable CA1309
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Security.Persistence;

public sealed partial class RelationalSecurityStore
{
    public async ValueTask<ProviderResult<IReadOnlyList<SecurityAuditRecord>>> SearchAuditAsync(SecurityAuditQuery query, CancellationToken cancellationToken)
    {
        if (query.Limit is < 1 or > 10001 || query.Action?.Length > 160 || query.Target?.Length > 1024 || query.Outcome?.Length > 64 ||
            query.FromUtc is { } from && query.UntilUtc is { } until && from >= until)
            return new(ProviderOutcome.Conflict, ErrorCode: "security.audit-query-invalid");
        await using var db = createContext();
        var rows = db.Audit.AsNoTracking().AsQueryable();
        if (query.FromUtc is { } start) { var ms = start.ToUnixTimeMilliseconds(); rows = rows.Where(row => row.OccurredAtUnixMilliseconds >= ms); }
        if (query.UntilUtc is { } end) { var ms = end.ToUnixTimeMilliseconds(); rows = rows.Where(row => row.OccurredAtUnixMilliseconds < ms); }
        if (query.Actor is { } actor) rows = rows.Where(row => row.ActorProviderId == actor.ProviderId && row.ActorSubjectId == actor.SubjectId);
        if (query.ActorProvider is { } actorProvider) rows = rows.Where(row => row.ActorProviderId == actorProvider);
        if (query.ActorId is { } actorId) rows = rows.Where(row => row.ActorSubjectId == actorId);
        if (!string.IsNullOrEmpty(query.Action)) rows = rows.Where(row => row.Action == query.Action);
        if (!string.IsNullOrEmpty(query.Target)) rows = rows.Where(row => row.Target == query.Target);
        if (!string.IsNullOrEmpty(query.Outcome)) rows = rows.Where(row => row.Outcome == query.Outcome);
        if (query.After is { } cursor) rows = rows.Where(row => row.OccurredAtUnixMilliseconds < cursor.OccurredAtUnixMilliseconds ||
            (row.OccurredAtUnixMilliseconds == cursor.OccurredAtUnixMilliseconds && string.Compare(row.Id, cursor.Id) < 0));
        var page = await rows.OrderByDescending(row => row.OccurredAtUnixMilliseconds).ThenByDescending(row => row.Id).Take(query.Limit).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new(ProviderOutcome.Succeeded, page.Select(row => new SecurityAuditRecord(row.Id,
            new(DateTimeOffset.FromUnixTimeMilliseconds(row.OccurredAtUnixMilliseconds), new(row.ActorProviderId, row.ActorSubjectId), row.Action, row.Target, row.Outcome))).ToArray());
    }

    public async ValueTask<ProviderResult<bool>> RecordAuditAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Action.Length > 160 || entry.Target.Length > 1024 || entry.Outcome.Length > 64) return new(ProviderOutcome.Conflict);
        await using var db = createContext();
        AddAudit(db, entry.Actor, entry.Action, entry.Target, entry.Outcome);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(ProviderOutcome.Succeeded, true);
    }
}

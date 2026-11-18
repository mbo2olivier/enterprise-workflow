using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Security.Persistence;

public sealed partial class RelationalSecurityStore
{
    public async ValueTask<string?> ReadCultureAsync(IdentityReference identity, CancellationToken cancellationToken)
    {
        await using var db = createContext();
        return await db.UserCultures.AsNoTracking().Where(row => row.ProviderId == identity.ProviderId && row.SubjectId == identity.SubjectId)
            .Select(row => row.Culture).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetCultureAsync(IdentityReference identity, string? culture, CancellationToken cancellationToken)
    {
        if (culture is not null)
        {
            if (culture.Length is 0 or > 32) throw new ArgumentException("Invalid culture.", nameof(culture));
            culture = CultureInfo.GetCultureInfo(culture).Name;
        }
        await using var db = createContext();
        var query = db.UserCultures.Where(row => row.ProviderId == identity.ProviderId && row.SubjectId == identity.SubjectId);
        if (culture is null) { await query.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false); return; }
        if (await query.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Culture, culture), cancellationToken).ConfigureAwait(false) > 0) return;
        db.UserCultures.Add(new() { ProviderId = identity.ProviderId, SubjectId = identity.SubjectId, Culture = culture });
        try { await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException)
        {
            // A concurrent first write may have inserted the preference. Other failures still propagate.
            if (await query.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Culture, culture), cancellationToken).ConfigureAwait(false) == 0) throw;
        }
    }
}

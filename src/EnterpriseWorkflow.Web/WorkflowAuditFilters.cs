using System.Globalization;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Web;

internal static class WorkflowAuditFilters
{
    public static SecurityAuditQuery Create(string? from, string? until, string? provider, string? actor,
        string? action, string? target, string? outcome, string? before, string? afterId, string timeZoneId, string? module = null, string? process = null, string? stage = null)
    {
        var identity = string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(actor) ? (IdentityReference?)null : new IdentityReference(provider!, actor!);
        var cursor = string.IsNullOrEmpty(before) && string.IsNullOrEmpty(afterId) ? null : new SecurityAuditCursor(long.Parse(before!, CultureInfo.InvariantCulture), afterId!);
        if (cursor is not null && !(Guid.TryParseExact(cursor.Id, "N", out _) || cursor.Id.StartsWith("s:", StringComparison.Ordinal) && Guid.TryParseExact(cursor.Id.AsSpan(2), "N", out _) || cursor.Id.StartsWith("w:", StringComparison.Ordinal) && long.TryParse(cursor.Id.AsSpan(2), out _))) throw new FormatException("Invalid audit cursor.");
        return new(Date(from, false), Date(until, true), identity, Empty(action), Empty(target), Empty(outcome), 50, cursor, Empty(module), Empty(process), Empty(stage), Empty(actor), Empty(provider));
        DateTimeOffset? Date(string? value, bool end)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var date = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (end) date = date.AddDays(1);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)));
        }
    }
    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

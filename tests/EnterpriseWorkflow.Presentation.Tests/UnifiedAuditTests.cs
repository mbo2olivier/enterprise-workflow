using System.Reflection;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;
using Microsoft.Extensions.Options;
using Xunit;
namespace EnterpriseWorkflow.Presentation.Tests;
public sealed class UnifiedAuditTests
{
    [Fact]
    public async Task EqualTimestampPagesMergeWithoutDuplicatesAndExportUsesTheSameBusinessFilters()
    {
        var profiles = DispatchProxy.Create<ISecurityProfileStore, SecurityRows>();
        var workflows = DispatchProxy.Create<IWorkflowStore, BusinessRows>();
        var authorization = new Authorization();
        var audit = new WorkflowAuditService(profiles, authorization, Options.Create(new WorkflowAuditOptions()), workflows);
        var actor = new IdentityReference("local", "admin"); var token = TestContext.Current.CancellationToken;
        var page = await audit.SearchAsync(actor, new(Limit: 2), token);
        Assert.Equal(2, page.Value!.Count); Assert.All(page.Value, row => Assert.StartsWith("w:", row.Id, StringComparison.Ordinal));
        var last = page.Value[^1];
        var next = await audit.SearchAsync(actor, new(Limit: 2, After: new(last.Entry.OccurredAtUtc.ToUnixTimeMilliseconds(), last.Id)), token);
        Assert.Equal(2, next.Value!.Count); Assert.All(next.Value, row => Assert.StartsWith("s:", row.Id, StringComparison.Ordinal));
        Assert.Empty(page.Value.Select(row => row.Id).Intersect(next.Value.Select(row => row.Id), StringComparer.Ordinal));
        var export = await audit.ExportAsync(actor, new(Process: "leave", Action: "decline"), token);
        Assert.Null(export.ErrorCode); Assert.NotNull(export.Content);
        Assert.Equal("leave", ((BusinessRows)(object)workflows).Last!.ProcessId);
        Assert.Equal("decline", ((BusinessRows)(object)workflows).Last!.Action);
        Assert.Contains("Author snapshot", System.Text.Encoding.UTF8.GetString(export.Content), StringComparison.Ordinal);
        authorization.Denied = true;
        Assert.Equal(ProviderOutcome.Forbidden, (await audit.SearchAsync(actor, new(), token)).Outcome);
        Assert.Equal("security.audit-export-denied", (await audit.ExportAsync(actor, new(), token)).ErrorCode);
    }
    private sealed class Authorization : IAuthorizationProvider
    {
        public bool Denied { get; set; }
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(!Denied, "test"));
    }
    private static readonly DateTimeOffset Date = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
#pragma warning disable CA2012
    public class SecurityRows : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ISecurityProfileStore.RecordAuditAsync)) return ValueTask.FromResult(new ProviderResult<bool>(ProviderOutcome.Succeeded, true));
            var query = (SecurityAuditQuery)args![0]!;
            IReadOnlyList<SecurityAuditRecord> rows = Enumerable.Range(1, 2).Select(index => new SecurityAuditRecord(index.ToString("D32", System.Globalization.CultureInfo.InvariantCulture), new(Date, new("local", "alice"), "security.session.revoke", "session", "succeeded")))
                .Where(row => (query.UntilUtc is null || row.Entry.OccurredAtUtc < query.UntilUtc) && (query.After is null || string.CompareOrdinal(row.Id, query.After.Id) < 0)).Take(query.Limit).ToArray();
            return ValueTask.FromResult(new ProviderResult<IReadOnlyList<SecurityAuditRecord>>(ProviderOutcome.Succeeded, rows));
        }
    }
    public class BusinessRows : DispatchProxy
    {
        public WorkflowAuditQuery? Last { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Last = (WorkflowAuditQuery)args![0]!;
            IReadOnlyList<WorkflowAuditRow> rows = Enumerable.Range(1, 2).Reverse().Select(index => new WorkflowAuditRow(index, Date, new(new("local"), "alice") { DisplayName = "Author snapshot" }, "decline", "HumanTaskCompleted", "succeeded", "module", "leave", "Leave", "review", "Review", "ABC1234567"))
                .Where(row => (Last.UntilUtc is null || row.Date < Last.UntilUtc) && (Last.BeforeTime is null || row.Sequence < Last.BeforeSequence)).Take(Last.Limit).ToArray();
            return ValueTask.FromResult(StoreResults.Succeeded(rows));
        }
    }
}

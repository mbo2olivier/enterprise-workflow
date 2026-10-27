using System.Collections.Immutable;
using System.Reflection;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace EnterpriseWorkflow.Presentation.Tests;

public sealed class WorkflowSearchAuthorizationTests
{
    [Fact]
    public async Task DeniedCandidatesDoNotHideLaterResultsAndCursorsAreBoundToActorAndFilters()
    {
        var store = DispatchProxy.Create<IWorkflowStore, CandidateStore>();
        var authorization = new Permissions();
        var portal = new WorkflowPortalService(new EmptyCatalog(), authorization, null!, store, null!, null!, authorization, null!,
            new Dictionary<string, IIdentityDirectory>(), Options.Create(new WorkflowPortalOptions()));
        var actor = new IdentityReference("local", "alice");
        var first = await portal.SearchInstancesAsync(actor, new(), TestContext.Current.CancellationToken);
        Assert.Empty(first.Items);
        Assert.NotNull(first.NextCursor);
        var second = await portal.SearchInstancesAsync(actor, new(Cursor: first.NextCursor), TestContext.Current.CancellationToken);
        Assert.Equal(25, second.Items.Count);
        Assert.All(second.Items, item => Assert.Equal("public", item.ProcessTitle));
        Assert.Equal("presentation.search-cursor-invalid", (await portal.SearchInstancesAsync(new("local", "bob"), new(Cursor: first.NextCursor), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal("presentation.search-cursor-invalid", (await portal.SearchInstancesAsync(actor, new(ProcessId: "public", Cursor: first.NextCursor), TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Equal("security.search-initiator-denied", (await portal.SearchInstancesAsync(actor, new(Initiator: actor), TestContext.Current.CancellationToken)).ErrorCode);
        authorization.Revoked = true;
        var afterRevocation = await portal.SearchInstancesAsync(actor, new(Cursor: first.NextCursor), TestContext.Current.CancellationToken);
        Assert.Empty(afterRevocation.Items);
    }
    private sealed class Permissions : IAuthorizationProvider, IWorkflowAuthorizationService
    {
        public bool Revoked { get; set; }
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(false, "test"));
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(!Revoked && request.Scope.WorkflowId == "public", "test"));
    }
    private sealed class EmptyCatalog : IWorkflowPresentationCatalog
    {
        public ImmutableArray<WorkflowProcessDescriptor> Processes => [];
        public bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process) { process = null; return false; }
        public bool TryResolveForm(WorkflowDefinition definition, WorkflowFormReference reference, out WorkflowFormRegistration? registration) { registration = null; return false; }
    }
#pragma warning disable CA2012 // DispatchProxy returns a boxed ValueTask, awaited exactly once by the service.
    public class CandidateStore : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IWorkflowStore.SearchInstancesAsync)) throw new NotSupportedException();
            var query = (WorkflowInstanceSearch)args![0]!;
            var start = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
            IReadOnlyList<WorkflowInstanceCandidate> rows = Enumerable.Range(0, 600).Select(index => new WorkflowInstanceCandidate(
                new(new(Guid.NewGuid()), new(index < 550 ? "private" : "public"), 1, WorkflowInstanceStatus.Waiting, 0, null, start.AddMilliseconds(-index), start), new(new("local"), "owner")))
                .Where(row => query.After is null || row.Summary.CreatedAtUtc.ToUnixTimeMilliseconds() < query.After.CreatedAtUnixMilliseconds).Take(query.Limit).ToArray();
            return ValueTask.FromResult(StoreResults.Succeeded(rows));
        }
    }
}

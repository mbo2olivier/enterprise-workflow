using System.Reflection;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;
using Xunit;
namespace EnterpriseWorkflow.Presentation.Tests;
public sealed class MediaLibraryPolicyTests
{
    [Fact]
    public async Task LibraryUsesMediaPoliciesWithoutRequiringReferencesInVisibleState()
    {
        var store = DispatchProxy.Create<IWorkflowStore, MediaStore>();
        var portal = DispatchProxy.Create<IWorkflowPortalService, Portal>();
        var library = new WorkflowMediaPortalService(store, null!, portal, null!, null!, new Permissions(), null!);
        var items = await library.ListAsync(new("local", "alice"), new(Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal("public.txt", Assert.Single(items).Name);
        ((Portal)(object)portal).Denied = true;
        Assert.Empty(await library.ListAsync(new("local", "alice"), new(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }
    private sealed class Permissions : IAuthorizationProvider
    {
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(false, "test"));
    }
#pragma warning disable CA2012
    public class MediaStore : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var id = new WorkflowInstanceId(Guid.NewGuid());
            var item = new WorkflowMediaRecord(new(Guid.NewGuid(), 1), "public.txt", "text/plain", 1, new string('a', 64), "key", new(new("local"), "owner"), id, null, null, "documents", false, [], WorkflowMediaStatus.Active, DateTimeOffset.UtcNow);
            IReadOnlyList<WorkflowMediaRecord> rows = [item, item with { Name = "hidden.txt", Hidden = true }, item with { Name = "restricted.txt", PermissionGroups = [["sensitive"]] }];
            return ValueTask.FromResult(StoreResults.Succeeded(rows));
        }
    }
    public class Portal : DispatchProxy
    {
        public bool Denied { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            WorkflowInstanceDetail? detail = Denied ? null : new(new(new(Guid.NewGuid()), "Test", 1, WorkflowInstanceStatus.Waiting, 0, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CanonicalJson.CreateObject("{}", 1024), new Dictionary<string, string>());
            return ValueTask.FromResult(detail);
        }
    }
}

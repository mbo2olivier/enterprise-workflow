using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Extensions.Tests;

public sealed class ModulePermissionTests
{
    [Fact]
    public async Task StageAndActionGroupsAreCumulativeAndFutureVersionsReusePermissions()
    {
        var actor = new IdentityReference("local", "reviewer");
        var permissions = new Permissions();
        var catalog = new Catalog();
        var historical = new Historical();
        var authorization = new ModulePermissionAuthorizationService(catalog, permissions, historical);
        var cancellationToken = TestContext.Current.CancellationToken;
        permissions.Values.UnionWith(["sample.review", "sample.senior"]);
        Assert.True((await authorization.AuthorizeAsync(new(actor, new("sample.request", 1, "review", new("approve"))), cancellationToken)).Allowed);
        Assert.False((await authorization.AuthorizeAsync(new(actor, new("sample.request", 1, "review", new("override"))), cancellationToken)).Allowed);
        permissions.Values.Add("sample.override");
        Assert.True((await authorization.AuthorizeAsync(new(actor, new("sample.request", 2, "review", new("override"))), cancellationToken)).Allowed);
        permissions.Values.Remove("sample.review");
        Assert.False((await authorization.AuthorizeAsync(new(actor, new("sample.request", 1, "review", new("approve"))), cancellationToken)).Allowed);
        Assert.Equal(0, historical.Calls);
        Assert.False((await authorization.AuthorizeAsync(new(actor, new("sample.request", 1, null, WorkflowActions.ReadInstance)), cancellationToken)).Allowed);
    }

    [Fact]
    public async Task HistoricalDefinitionsKeepTheirExactGrantService()
    {
        var historical = new Historical();
        var authorization = new ModulePermissionAuthorizationService(new Catalog(legacy: true), new Permissions(), historical);
        Assert.True((await authorization.AuthorizeAsync(new(new("local", "person"), new("sample.request", 1, null, WorkflowActions.Start)), TestContext.Current.CancellationToken)).Allowed);
        Assert.Equal(1, historical.Calls);
    }

    private sealed class Permissions : IAuthorizationProvider
    {
        public HashSet<string> Values { get; } = new(StringComparer.Ordinal);
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuthorizationDecision(Values.Contains(request.Permission.Value), "test"));
    }
    private sealed class Historical : IWorkflowAuthorizationService
    {
        public int Calls { get; private set; }
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(new AuthorizationDecision(true, "historical"));
        }
    }
    private sealed class Catalog(bool legacy = false) : IWorkflowPresentationCatalog
    {
        public ImmutableArray<WorkflowProcessDescriptor> Processes => [];
        public bool TryResolveForm(WorkflowDefinition definition, WorkflowFormReference reference, out WorkflowFormRegistration? registration) { registration = null; return false; }
        public bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process)
        {
            var policy = new WorkflowStagePolicy
            {
                Permissions = [["sample.review"], ["sample.senior", "sample.deputy"]],
                Actions = new() { ["approve"] = new() { Label = "Approuver" }, ["override"] = new() { Label = "Dérogation", Permissions = [["sample.override"]] } },
            };
            var definition = WorkflowBuilder.Create("sample.request", version)
                .Start("start", legacy ? "{}" : new WorkflowStagePolicy { Permissions = [["sample.start"]] }.ToConfigurationJson())
                .HumanTask("review", HumanTaskKind.Approval, HumanTaskAssignmentMode.EligiblePool, "form", 1, "handler",
                    [new("approve", "approve"), new("override", "override")], configurationJson: legacy ? "{}" : policy.ToConfigurationJson())
                .End("end").Then("start", "review").On("review", "approve", "end").On("review", "override", "end").Validate().Definition!;
            process = new(definition, new(new("sample"), "1.0.0", new string('a', 64)), "Test", "Test", new(new("form"), 1), 1, [], null);
            return true;
        }
    }
}

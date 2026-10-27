using System.Collections.Immutable;
using System.Reflection;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Sdk;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace EnterpriseWorkflow.Presentation.Tests;
public sealed class InstancePresentationTests
{
    [Fact]
    public async Task DedicatedCollectionsAreProjectedBeforeExtractionAndNeverAppearInTheStateTable()
    {
        var comments = new WorkflowStateField { Name = "comments", Label = "Avis RH", Kind = "array", PresentationRole = "comments", Item = new() { Kind = "object", Fields = [new() { Name = "text", Kind = "string" }, new() { Name = "actorDisplayName", Kind = "string" }] } };
        var contract = new WorkflowStateContract(1, new() { Kind = "object", Fields = [comments,
            comments with { Name = "secret", Label = "Secret", PermissionGroups = [["sensitive"]] },
            new() { Name = "details", Kind = "object", Fields = [new() { Name = "file", Kind = "object", PresentationRole = "media", Fields = [] }] }] });
        var configuration = new WorkflowStagePolicy { StateContract = contract }.ToConfigurationJson();
        var definition = WorkflowBuilder.Create("test", 1).Start("start", configuration).End("end").Then("start", "end").Validate().Definition!;
        var store = DispatchProxy.Create<IWorkflowStore, SnapshotStore>();
        var now = DateTimeOffset.UtcNow;
        ((SnapshotStore)(object)store).Snapshot = new(new(new(Guid.NewGuid()), new("test"), 1, WorkflowInstanceStatus.Waiting, 0, null, now, now), definition,
            new(new("local"), "alice"), WorkflowState.Create("""{"comments":[{"text":"Visible","actorDisplayName":"Auteur"}],"secret":[{"text":"Must not leak","actorDisplayName":"Secret"}],"details":{"file":{}}}""", 1));
        var permissions = new Permissions();
        var portal = new WorkflowPortalService(new EmptyCatalog(), permissions, null!, store, null!, null!, permissions, null!, new Dictionary<string, IIdentityDirectory>(), Options.Create(new WorkflowPortalOptions()));
        var detail = await portal.ReadInstanceAsync(new("local", "reader"), new(Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        var group = Assert.Single(detail.CommentGroups!);
        Assert.Equal("Avis RH", group.Label); Assert.Equal("Visible", Assert.Single(group.Comments).Text);
        Assert.DoesNotContain("comments", detail.VisibleState.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("file", detail.VisibleState.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", detail.VisibleState.CanonicalText, StringComparison.Ordinal);
        permissions.Denied = true;
        Assert.Null(await portal.ReadInstanceAsync(new("local", "reader"), new(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData(WorkflowCompletionOutcome.Succeeded, "Terminée", "success")]
    [InlineData(WorkflowCompletionOutcome.Declined, "Refusée", "warning")]
    [InlineData(WorkflowCompletionOutcome.Cancelled, "Annulée", "warning")]
    [InlineData(null, "Terminée", "neutral")]
    public async Task TerminalBusinessResultAndStageComeFromTheReachedEnd(WorkflowCompletionOutcome? outcome, string label, string style)
    {
        var contract = new WorkflowStateContract(1, new() { Kind = "object", Fields = [] });
        var definition = WorkflowBuilder.Create("test", 1)
            .Start("start", new WorkflowStagePolicy { StateContract = contract }.ToConfigurationJson())
            .End("end", new WorkflowStagePolicy { CompletionOutcome = outcome }.ToConfigurationJson(), "Décision finale")
            .Then("start", "end").Validate().Definition!;
        var store = DispatchProxy.Create<IWorkflowStore, SnapshotStore>();
        var now = DateTimeOffset.UtcNow;
        ((SnapshotStore)(object)store).Snapshot = new(new(new(Guid.NewGuid()), new("test"), 1, WorkflowInstanceStatus.Completed, 0, null, now, now,
            Positions: [new("start", NodeExecutionStatus.Succeeded), new("end", NodeExecutionStatus.Succeeded)]), definition,
            new(new("local"), "alice"), WorkflowState.Create("{}", 1));
        var permissions = new Permissions();
        var portal = new WorkflowPortalService(new EmptyCatalog(), permissions, null!, store, null!, null!, permissions, null!, new Dictionary<string, IIdentityDirectory>(), Options.Create(new WorkflowPortalOptions()));
        var detail = await portal.ReadInstanceAsync(new("local", "reader"), new(Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        Assert.Equal(label, detail.Instance.StatusLabel);
        Assert.Equal(style, detail.Instance.StatusClass);
        Assert.Equal("end", detail.Instance.CurrentStageId);
        Assert.Equal("Décision finale", detail.Instance.CurrentStageLabel);
    }

    private sealed class Permissions : IAuthorizationProvider, IWorkflowAuthorizationService
    {
        public bool Denied { get; set; }
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(false, "test"));
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(!Denied, "test"));
    }
    private sealed class EmptyCatalog : IWorkflowPresentationCatalog
    {
        public ImmutableArray<WorkflowProcessDescriptor> Processes => [];
        public bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process) { process = null; return false; }
        public bool TryResolveForm(WorkflowDefinition definition, WorkflowFormReference reference, out WorkflowFormRegistration? registration) { registration = null; return false; }
    }
#pragma warning disable CA2012
    public class SnapshotStore : DispatchProxy
    {
        public WorkflowInstanceReadSnapshot Snapshot { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name == nameof(IWorkflowStore.ReadInstanceAsync) ? ValueTask.FromResult(StoreResults.Succeeded(Snapshot)) : throw new NotSupportedException();
    }
}

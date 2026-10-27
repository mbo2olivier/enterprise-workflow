using System.Text.Json.Serialization;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class TypedAuthoringTests
{
    [Fact]
    public void EveryProcessRequiresATypedStateAndAnExplicitInitializer()
    {
        var missing = Base(); missing.AddProcess(process => process.WithId("missing"));
        Assert.Throws<InvalidOperationException>(() => Publish(missing));
        var noInitializer = Base(); noInitializer.AddProcess(process => process.WithState<EmptyState>().WithId("missing").WithStateSchemaVersion(1).RequiresPermission("act").StartWithForm("form").Then("end").End("end"));
        Assert.Throws<InvalidOperationException>(() => Publish(noInitializer));
        var empty = Base(); empty.AddProcess(process => process.WithState<EmptyState>().InitializedBy<Initializer>().WithId("empty").WithStateSchemaVersion(1).RequiresPermission("act").StartWithForm("form").Then("end").End("end"));
        var definition = Assert.Single(Publish(empty).Processes).DefinitionFactory(new(new("typed"), "1.0.0", new string('a', 64)));
        Assert.Empty(WorkflowStateContract.From(definition)!.Root.Fields);
    }
    [Fact]
    public void OnlySerializedStatePathsCanReceiveApprovalComments()
    {
        void Configure(WorkflowTypedActionBuilder<CommentState> action) => action.RequireCommentAt(state => state.Comments).Then("end");
        var module = WithApproval(Configure); var definition = Assert.Single(Publish(module).Processes).DefinitionFactory(new(new("typed"), "1.0.0", new string('a', 64)));
        Assert.Equal("discussion", Assert.Single(WorkflowStagePolicy.Read(definition.Nodes.Single(node => node.Id.Value == "review").Configuration)!.Actions["approve"].CommentPath!));
        Assert.Throws<InvalidOperationException>(() => WithApproval(action => action.RequireCommentAt(state => state.Comments.ToList()).Then("end")));
        Assert.Throws<InvalidOperationException>(() => Base().AddProcess(process =>
        {
            var flow = process.WithState<IgnoredState>().InitializedBy<Initializer>();
            flow.WithId("ignored").WithStateSchemaVersion(1).RequiresPermission("act").StartWithForm("form").Then("review");
            flow.Approval("review", "Review", task => task.RequiresPermission("act").ClaimedByEligibleParticipant().Actions(actions => actions.Add("approve", "Approve", action => action.RequireCommentAt(state => state.Ignored).Then("end"))));
            flow.End("end");
        }));
    }
    [Fact]
    public void ComputedStatePropertiesAreRejectedInsteadOfLosingDataOnTypedReads()
    {
        Assert.Throws<InvalidOperationException>(() => WorkflowStateContractCompiler.Compile<ComputedState>(1));
    }
    public sealed class ComputedState { public string Value { get; } = Guid.NewGuid().ToString("N"); }

    private static WorkflowModuleBuilder WithApproval(Action<WorkflowTypedActionBuilder<CommentState>> configure)
    {
        var module = Base(); module.AddProcess(process =>
        {
            var flow = process.WithState<CommentState>().InitializedBy<Initializer>();
            flow.WithId("request").WithStateSchemaVersion(1).RequiresPermission("act").StartWithForm("form").Then("review");
            flow.Approval("review", "Review", task => task.RequiresPermission("act").ClaimedByEligibleParticipant().Actions(actions => actions.Add("approve", "Approve", configure)));
            flow.End("end");
        }); return module;
    }
    private static WorkflowModuleBuilder Base() => new WorkflowModuleBuilder(new("typed", "1.0.0", 1)).AddHumanTask<Handler>("submit")
        .AddForm("form", "Form", form => form.HandledBy<Handler>().Fields(fields => fields.Text("value", "Value")));
    private static WorkflowPresentationRegistryBuilder Publish(WorkflowModuleBuilder module)
    {
        var registry = new WorkflowPresentationRegistryBuilder(); module.ConfigurePresentation(registry, new WorkflowPermissionRegistryBuilder(module.Info).Add("act", "Act").Permissions); return registry;
    }
    public sealed class EmptyState;
    public sealed class CommentState
    {
        [JsonPropertyName("discussion")] public List<WorkflowComment> Comments { get; init; } = [];
    }
    public sealed class IgnoredState
    {
        [JsonIgnore] public List<WorkflowComment> Ignored { get; init; } = [];
    }
    private sealed class Initializer : IWorkflowStateInitializer
    {
        public ValueTask<WorkflowStateInitializationResult> InitializeAsync(WorkflowStateInitializationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(new WorkflowStateInitializationResult(CanonicalJson.CreateObject("{}", 1024)));
    }
    private sealed class Handler : IHumanTaskCompletionHandler
    {
        public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken) => ValueTask.FromResult(HumanTaskCompletionResult.Success());
    }
}

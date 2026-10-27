using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class FluentAuthoringTests
{
    [Fact]
    public void ReferencesResolveAfterCollectionAndPoliciesRoundTripCanonically()
    {
        var module = new WorkflowModuleBuilder(new("sample.leave", "2.0.0", 4));
        WorkflowTypedActionBuilder<AuthorState>? mutableAction = null;
        module.AddProcess(process =>
        {
            var flow = process.WithState<AuthorState>().InitializedBy<AuthorInitializer>();
            flow.WithId("request").WithStateSchemaVersion(3).RequiresPermission("request")
                .StartWithForm("request-form").Then("review");
            flow.Approval("review", "Examiner", stage => stage
                .RequiresPermission("review").RequiresAnyPermission("senior", "deputy")
                .ClaimedByEligibleParticipant().ShowFields("reason")
                .Actions(actions => actions.Add("approve", "Approuver", action => { mutableAction = action; action.Then("end"); })));
            flow.End("end");
        });
        module.AddForm("request-form", "Demande", form => form.HandledBy<Handler>()
            .ReadableBy(WorkflowFieldAudience.TaskParticipant).Fields(fields => fields.Text("reason", "Motif").Required()));
        module.AddHumanTask<Handler>("submit");
        var permissions = new WorkflowPermissionRegistryBuilder(module.Info);
        foreach (var name in new[] { "request", "review", "senior", "deputy" }) permissions.Add(name, name);
        var registry = new WorkflowPresentationRegistryBuilder();
        module.ConfigurePresentation(registry, permissions.Permissions);
        var processDeclaration = Assert.Single(registry.Processes);
        Assert.Equal(3, processDeclaration.InitialStateSchemaVersion);
        var artifact = new WorkflowArtifactReference(new("sample.leave"), "2.0.0", new string('a', 64));
        var definition = processDeclaration.DefinitionFactory(artifact);
        var stage = definition.Nodes.Single(node => node.Id.Value == "review");
        var policy = Assert.IsType<WorkflowStagePolicy>(WorkflowStagePolicy.Read(stage.Configuration));
        Assert.Contains(policy.Permissions, group => group.SequenceEqual(new[] { "sample.leave.deputy", "sample.leave.senior" }));
        Assert.Contains(policy.Permissions, group => group.SequenceEqual(new[] { "sample.leave.review" }));
        Assert.Equal(4, stage.HumanTask!.Form.Version);
        Assert.Equal("sample.leave.standard.complete", stage.HumanTask.CompletionHandler.Id.Value);
        mutableAction!.Then("missing-after-snapshot");
        Assert.Equal(definition.CanonicalJson, processDeclaration.DefinitionFactory(artifact).CanonicalJson);
        var descriptor = new WorkflowProcessDescriptor(definition, artifact, "Demande", "Demande", processDeclaration.StartForm, 3, [], null);
        var next = new WorkflowModuleBuilder(new("sample.leave", "3.0.0", 5)).UseProcess(descriptor);
        var reused = new WorkflowPresentationRegistryBuilder();
        next.ConfigurePresentation(reused, []);
        var exposed = Assert.Single(reused.Processes);
        Assert.Same(definition, exposed.DefinitionFactory(new(new("sample.leave"), "3.0.0", new string('b', 64))));
        Assert.Equal(artifact, reused.ReferencedProcessOwner(definition.Id.Value, definition.Version));
    }

    [Fact]
    public void MissingStateSchemaAndUndeclaredPermissionsAreRejected()
    {
        var module = new WorkflowModuleBuilder(new("sample", "1.0.0", 1));
        module.AddHumanTask<Handler>("handler").AddForm("form", "Formulaire", form => form.HandledBy<Handler>()
            .Fields(fields => fields.Text("reason", "Motif")));
        module.AddProcess(process => process.WithState<AuthorState>().InitializedBy<AuthorInitializer>().WithId("test").RequiresPermission("unknown").StartWithForm("form").Then("end").End("end"));
        var registry = new WorkflowPresentationRegistryBuilder();
        Assert.Throws<InvalidOperationException>(() => module.ConfigurePresentation(registry, []));
    }

    [Fact]
    public void FormRequiresAnExplicitRegisteredDefaultHandler()
    {
        var module = new WorkflowModuleBuilder(new("sample", "1.0.0", 1));
        module.AddForm("form", "Formulaire", form => form.Fields(fields => fields.Text("reason", "Motif")));
        Assert.Throws<InvalidOperationException>(() => module.ConfigurePresentation(new(), []));
    }

    [Fact]
    public void InvalidPolicyFailsCompilationInsteadOfFallingBackToLegacyAuthorization()
    {
        var result = WorkflowBuilder.Create("sample", 1)
            .Start("start", "{\"$workflow\":{\"Version\":99}}")
            .End("end").Then("start", "end").Validate();
        Assert.False(result.IsPublishable);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "EW1120");
    }

    [Fact]
    public void AssignmentMustSelectTheDirectHumanDestination()
    {
        var module = new WorkflowModuleBuilder(new("sample", "1.0.0", 1));
        module.AddHumanTask<Handler>("submit").AddForm("form", "Formulaire", form => form.HandledBy<Handler>()
            .Fields(fields => fields.Text("reason", "Motif")));
        module.AddProcess(process =>
        {
            var flow = process.WithState<AuthorState>().InitializedBy<AuthorInitializer>();
            flow.WithId("test").WithStateSchemaVersion(1).RequiresPermission("act").StartWithForm("form").Then("review");
            flow.Approval("review", "Examen", stage => stage.RequiresPermission("act").ClaimedByEligibleParticipant()
                .Actions(actions => actions.Add("approve", "Approuver", action => action.SelectAssigneeFor("later").Then("end"))));
            flow.End("end");
        });
        var permissions = new WorkflowPermissionRegistryBuilder(module.Info).Add("act", "Agir");
        Assert.Throws<InvalidOperationException>(() => module.ConfigurePresentation(new(), permissions.Permissions));
    }

    public sealed class AuthorState
    {
        [System.Text.Json.Serialization.JsonPropertyName("reason")] public string? Reason { get; set; }
    }
    private sealed class AuthorInitializer : IWorkflowStateInitializer
    {
        public ValueTask<WorkflowStateInitializationResult> InitializeAsync(WorkflowStateInitializationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(new WorkflowStateInitializationResult(context.Submission));
    }

    private sealed class Handler : IHumanTaskCompletionHandler
    {
        public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(HumanTaskCompletionResult.Success());
    }
}

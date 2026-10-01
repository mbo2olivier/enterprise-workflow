using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class WorkflowBuilderTests
{
    [Fact]
    public void SequentialWorkflowProducesCanonicalDefinition()
    {
        var result = WorkflowBuilder.Create("leave.request", 1)
            .WithDisplayLabel("Leave request")
            .Start("start")
            .Service("notify", "leave.notify", configurationJson: "{\"template\":\"created\"}")
            .End("end")
            .Then("start", "notify")
            .Then("notify", "end")
            .Validate();

        Assert.True(result.IsPublishable);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Definition);
        Assert.Equal(WorkflowDefinition.SchemaVersion, 1);
        Assert.Equal(64, result.Definition.Sha256.Length);
        Assert.Equal(["end", "notify", "start"], result.Definition.Nodes.Select(item => item.Id.Value));
    }

    [Fact]
    public void ExclusiveDecisionCanConvergeOnOneEnd()
    {
        var result = WorkflowBuilder.Create("leave.decision", 1)
            .Start("start")
            .Decision("decision", "leave.evaluate", ["approved", "rejected"])
            .End("end")
            .Then("start", "decision")
            .On("decision", "approved", "end")
            .On("decision", "rejected", "end")
            .Validate();

        Assert.True(result.IsPublishable);
        Assert.Equal(3, result.Definition!.Transitions.Length);
    }

    [Fact]
    public void EquivalentInsertionOrdersProduceSameCanonicalJsonAndHash()
    {
        var first = WorkflowBuilder.Create("deterministic", 1)
            .Start("start", "{\"z\":1,\"a\":2}")
            .Decision("choice", "evaluate", ["yes", "no"])
            .End("end")
            .Then("start", "choice")
            .On("choice", "yes", "end")
            .On("choice", "no", "end")
            .Validate();

        var second = WorkflowBuilder.Create("deterministic", 1)
            .End("end")
            .Decision("choice", "evaluate", ["no", "yes"])
            .Start("start", "{\"a\":2,\"z\":1}")
            .On("choice", "no", "end")
            .Then("start", "choice")
            .On("choice", "yes", "end")
            .Validate();

        Assert.True(first.IsPublishable);
        Assert.True(second.IsPublishable);
        Assert.Equal(first.Definition!.CanonicalJson, second.Definition!.CanonicalJson);
        Assert.Equal(first.Definition.Sha256, second.Definition.Sha256);
    }

    [Fact]
    public void SemanticChangeProducesDifferentHash()
    {
        var first = BuildServiceWorkflow("{\"value\":1}");
        var second = BuildServiceWorkflow("{\"value\":2}");

        Assert.NotEqual(first.Sha256, second.Sha256);
    }

    [Fact]
    public void ArtifactHashIsDistinctAndParticipatesInDefinitionHash()
    {
        var first = WorkflowBuilder.Create("artifact", 1)
            .RequiresArtifact("leave.module", "1.0.0", new string('a', 64))
            .Start("start")
            .End("end")
            .Then("start", "end")
            .Validate();
        var second = WorkflowBuilder.Create("artifact", 1)
            .RequiresArtifact("leave.module", "1.0.0", new string('b', 64))
            .Start("start")
            .End("end")
            .Then("start", "end")
            .Validate();

        Assert.Equal(new string('a', 64), first.Definition!.Artifacts[0].Sha256);
        Assert.NotEqual(first.Definition.Sha256, second.Definition!.Sha256);
    }

    [Fact]
    public void DraftIsDetachedFromLaterBuilderChanges()
    {
        var outcomes = new List<string> { "yes" };
        var builder = WorkflowBuilder.Create("snapshot", 1)
            .Start("start")
            .Decision("decision", "evaluate", outcomes)
            .End("end");
        var draft = builder.BuildDraft();

        outcomes.Add("no");
        builder.End("another-end");

        Assert.Equal(3, draft.Nodes.Length);
        Assert.Single(draft.Nodes.Single(item => item.Id == "decision").DeclaredOutcomes);
    }

    private static WorkflowDefinition BuildServiceWorkflow(string configurationJson)
    {
        var result = WorkflowBuilder.Create("hash-change", 1)
            .Start("start")
            .Service("service", "execute", configurationJson: configurationJson)
            .End("end")
            .Then("start", "service")
            .Then("service", "end")
            .Validate();

        return Assert.IsType<WorkflowDefinition>(result.Definition);
    }
}

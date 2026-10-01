using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Diagnostics;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Validation;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class WorkflowValidationTests
{
    [Theory]
    [InlineData("_invalid")]
    [InlineData("contains space")]
    [InlineData("éclair")]
    [InlineData("")]
    public void InvalidDefinitionIdentifiersAreLocalized(string definitionId)
    {
        var result = ValidSkeleton(definitionId).Validate();

        AssertDiagnostic(result, "EW1001", "definition.id");
    }

    [Fact]
    public void TechnicalIdentifiersAreCaseSensitive()
    {
        var result = WorkflowBuilder.Create("case-sensitive", 1)
            .Start("Start")
            .End("start")
            .Then("Start", "start")
            .Validate();

        Assert.True(result.IsPublishable);
        Assert.Equal(2, result.Definition!.Nodes.Length);
    }

    [Fact]
    public void DuplicateNodesAreRejected()
    {
        var result = WorkflowBuilder.Create("duplicate", 1)
            .Start("start")
            .End("end")
            .End("end")
            .Then("start", "end")
            .Validate();

        AssertDiagnostic(result, "EW1102", "nodes[2].id");
    }

    [Fact]
    public void MissingReferencesAndUnreachableNodesAreRejected()
    {
        var result = WorkflowBuilder.Create("references", 1)
            .Start("start")
            .End("end")
            .Service("orphan", "execute")
            .Then("start", "missing")
            .Then("orphan", "end")
            .Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1313");
        Assert.Contains(result.Diagnostics, item => item.Code == "EW1320" && item.Location == "node:orphan");
    }

    [Fact]
    public void CyclesAreRejected()
    {
        var result = WorkflowBuilder.Create("cycle", 1)
            .Start("start")
            .Service("service", "execute")
            .End("end")
            .Then("start", "service")
            .Then("service", "start")
            .Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1321");
        Assert.False(result.IsPublishable);
    }

    [Fact]
    public void DecisionRequiresEveryOutcomeOrExplicitDefault()
    {
        var result = WorkflowBuilder.Create("missing-outcome", 1)
            .Start("start")
            .Decision("decision", "evaluate", ["yes", "no"])
            .End("end")
            .Then("start", "decision")
            .On("decision", "yes", "end")
            .Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1319" && item.Message.Contains("no", StringComparison.Ordinal));
    }

    [Fact]
    public void ExplicitDefaultCoversUnknownAndUnconnectedOutcomes()
    {
        var result = WorkflowBuilder.Create("default-outcome", 1)
            .Start("start")
            .Decision("decision", "evaluate", ["yes", "no"])
            .End("end")
            .Then("start", "decision")
            .On("decision", "yes", "end")
            .Otherwise("decision", "end")
            .Validate();

        Assert.True(result.IsPublishable);
    }

    [Fact]
    public void AmbiguousDecisionBranchesAreRejected()
    {
        var result = WorkflowBuilder.Create("ambiguous", 1)
            .Start("start")
            .Decision("decision", "evaluate", ["yes"])
            .End("end")
            .Then("start", "decision")
            .On("decision", "yes", "end")
            .On("decision", "yes", "end")
            .Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1316");
    }

    [Fact]
    public void UnknownNodeTypePreventsPublication()
    {
        var result = WorkflowBuilder.Create("unknown-type", 1)
            .Start("start")
            .CustomNode("custom", "custom.node", 1, WorkflowNodeRole.Service, "execute", 1, "{}")
            .End("end")
            .Then("start", "custom")
            .Then("custom", "end")
            .Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1401");
    }

    [Fact]
    public void ApplicationCatalogValidatesCustomConfiguration()
    {
        var customType = new NodeTypeReference(new TechnicalId("custom.node"), 1);
        var catalog = NodeTypeCatalog.BuiltIns.With(
            new NodeTypeDescriptor(customType, WorkflowNodeRole.Service, new RejectConfigurationValidator()));
        var result = WorkflowBuilder.Create("custom-config", 1)
            .Start("start")
            .CustomNode("custom", "custom.node", 1, WorkflowNodeRole.Service, "execute", 1, "{}")
            .End("end")
            .Then("start", "custom")
            .Then("custom", "end")
            .Validate(catalog);

        Assert.Contains(result.Diagnostics, item => item.Code == "CUSTOM001");
        Assert.False(result.IsPublishable);
    }

    [Theory]
    [InlineData("{\"duplicate\":1,\"duplicate\":2}", "EW1205")]
    [InlineData("{/* comment */\"value\":1}", "EW1203")]
    [InlineData("[]", "EW1204")]
    public void InvalidDurableJsonIsRejected(string json, string code)
    {
        var result = WorkflowBuilder.Create("json", 1)
            .Start("start", json)
            .End("end")
            .Then("start", "end")
            .Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == code);
    }

    [Fact]
    public void JsonLimitsApplyToReceivedAndCanonicalForms()
    {
        var result = WorkflowBuilder.Create("json-limit", 1)
            .Start("start", "{\"value\":\"1234567890\"}")
            .End("end")
            .Then("start", "end")
            .Validate(limits: WorkflowDefinitionLimits.Default with { MaximumNodeConfigurationBytes = 10 });

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1202");
    }

    [Fact]
    public void DefinitionAndNodeCountLimitsAreExplicit()
    {
        var builder = ValidSkeleton("limits");
        var nodeLimit = builder.Validate(limits: WorkflowDefinitionLimits.Default with { MaximumNodeCount = 1 });
        var byteLimit = builder.Validate(limits: WorkflowDefinitionLimits.Default with { MaximumDefinitionBytes = 20 });

        Assert.Contains(nodeLimit.Diagnostics, item => item.Code == "EW1003");
        Assert.Contains(byteLimit.Diagnostics, item => item.Code == "EW1501");
    }

    [Fact]
    public void JsonDepthLimitIsEnforced()
    {
        var result = WorkflowBuilder.Create("depth", 1)
            .Start("start", "{\"a\":{\"b\":{\"c\":1}}}")
            .End("end")
            .Then("start", "end")
            .Validate(limits: WorkflowDefinitionLimits.Default with { MaximumJsonDepth = 2 });

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1203");
    }

    [Fact]
    public void InvalidArtifactReferenceIsRejected()
    {
        var result = ValidSkeleton("artifact-invalid")
            .RequiresArtifact("module", "1.0.0", "not-a-hash")
            .Validate();

        Assert.Contains(result.Diagnostics, item => item.Code == "EW1010");
    }

    [Fact]
    public void CanonicalJsonSortsPropertiesWithoutChangingNumberText()
    {
        const string largeNumber = "123456789012345678901234567890";
        var json = CanonicalJson.CreateObject($"{{\"z\":{largeNumber},\"a\":1}}", 1_024);

        Assert.Equal($"{{\"a\":1,\"z\":{largeNumber}}}", json.CanonicalText);
    }

    [Fact]
    public void StateUpdateIsExplicitAndDetachedFromSourceDocument()
    {
        using var document = System.Text.Json.JsonDocument.Parse("{\"value\":1}");
        var replacement = NodeStateUpdate.Replace(document.RootElement);

        Assert.False(NodeStateUpdate.Unchanged.ReplacesState);
        Assert.True(replacement.ReplacesState);
        Assert.Equal(1, replacement.GetReplacement()!.Value.GetProperty("value").GetInt32());
    }

    [Fact]
    public void VersionedStateUsesTheDedicatedStateLimit()
    {
        var state = WorkflowState.Create("{\"value\":1}", 3);

        Assert.Equal(3, state.SchemaVersion);
        Assert.Equal("{\"value\":1}", state.Value.CanonicalText);
        Assert.Throws<FormatException>(() => WorkflowState.Create(
            "{\"value\":\"too large\"}",
            1,
            WorkflowDefinitionLimits.Default with { MaximumStateBytes = 10 }));
    }

    private static WorkflowBuilder ValidSkeleton(string definitionId) =>
        WorkflowBuilder.Create(definitionId, 1)
            .Start("start")
            .End("end")
            .Then("start", "end");

    private static void AssertDiagnostic(WorkflowCompilationResult result, string code, string location)
    {
        Assert.Contains(result.Diagnostics, item => item.Code == code && item.Location == location);
        Assert.False(result.IsPublishable);
    }

    private sealed class RejectConfigurationValidator : INodeConfigurationValidator
    {
        public IEnumerable<WorkflowDiagnostic> Validate(CanonicalJson configuration, string nodeLocation)
        {
            yield return new WorkflowDiagnostic(
                "CUSTOM001",
                WorkflowDiagnosticSeverity.Error,
                $"{nodeLocation}.configuration",
                $"Rejected {configuration.CanonicalByteCount} bytes for the test.");
        }
    }
}

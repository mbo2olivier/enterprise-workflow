using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class WorkflowStateContractTests
{
    [Fact]
    public void ContractValidatesPermanentAndContextualRulesWithoutCouplingFormNames()
    {
        var contract = WorkflowStateContractCompiler.Compile<RequestState>(3);
        var state = Json("""{"reference":"ABC","ApprovedAmount":null,"Internal":"hidden","Details":{"Public":"ok","Private":"secret"}}""");
        Assert.Empty(contract.Validate(state));
        Assert.Contains("$.ApprovedAmount", contract.Validate(state, ["approval"]));
        Assert.Contains("$.reference", contract.Validate(Json("""{"reference":"A","Details":{"Public":"ok"}}""")));
        Assert.Contains("$.unknown", contract.Validate(Json("""{"reference":"ABC","Details":{"Public":"ok"},"unknown":true}""")));
        Assert.Equal(3, contract.Version);
    }

    [Fact]
    public void NestedFieldsCannotBypassHiddenOrConjunctivePermissionGroups()
    {
        var contract = WorkflowStateContractCompiler.Compile<RequestState>(1);
        var state = Json("""{"reference":"ABC","ApprovedAmount":10,"Internal":"hidden","Details":{"Public":"ok","Private":"secret"}}""");
        var publicView = contract.Project(state, new HashSet<string>(StringComparer.Ordinal));
        Assert.Equal("""{"Details":{"Public":"ok"},"reference":"ABC"}""", publicView.CanonicalText);
        var limited = contract.Project(state, new HashSet<string>(["finance"], StringComparer.Ordinal));
        Assert.False(limited.ToJsonElement().TryGetProperty("ApprovedAmount", out _));
        var authorized = contract.Project(state, new HashSet<string>(["finance", "senior", "sensitive"], StringComparer.Ordinal));
        Assert.Equal(10, authorized.ToJsonElement().GetProperty("ApprovedAmount").GetInt32());
        Assert.False(authorized.ToJsonElement().TryGetProperty("Internal", out _));
        Assert.Equal("secret", authorized.ToJsonElement().GetProperty("Details").GetProperty("Private").GetString());
    }

    [Fact]
    public void MalformedComplexValueCannotBypassTheDeclaredScalarProjection()
    {
        var contract = WorkflowStateContractCompiler.Compile<RequestState>(1);
        var projected = contract.Project(Json("""{"reference":{"secret":"must-not-leak"},"Details":{"Public":"ok"}}"""), new HashSet<string>(StringComparer.Ordinal));
        Assert.DoesNotContain("must-not-leak", projected.CanonicalText, StringComparison.Ordinal);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, projected.ToJsonElement().GetProperty("reference").ValueKind);
    }

    [Fact]
    public void RecursiveDtosAreRejectedBeforePublication() => Assert.Throws<InvalidOperationException>(() => WorkflowStateContractCompiler.Compile<RecursiveState>(1));

    [Fact]
    public void DedicatedCollectionsHaveMetadataAndInvalidCommentTargetsAreRejected()
    {
        var contract = WorkflowStateContractCompiler.Compile<DedicatedState>(1);
        Assert.Equal("comments", contract.Root.Fields.Single(field => field.Name == "Comments").PresentationRole);
        Assert.Equal("media", contract.Root.Fields.Single(field => field.Name == "Files").PresentationRole);
        Assert.Throws<InvalidOperationException>(() => WorkflowStateContractCompiler.Compile<InvalidComments>(1));
    }
    public sealed class DedicatedState
    {
        [CommentState] public IEnumerable<WorkflowComment> Comments { get; init; } = [];
        public List<WorkflowMediaReference> Files { get; init; } = [];
    }
    public sealed class InvalidComments { [CommentState] public List<string> Comments { get; init; } = []; }

    private static CanonicalJson Json(string value) => CanonicalJson.CreateObject(value, 256 * 1024);
    public sealed class RequestState
    {
        [JsonPropertyName("reference"), Required, StringLength(20, MinimumLength = 3)]
        public string Reference { get; init; } = string.Empty;
        [RequiredFor("approval"), VisibleFor("finance"), VisibleFor("senior", "deputy")]
        public decimal? ApprovedAmount { get; init; }
        [HiddenField] public string? Internal { get; init; }
        public RequestDetails Details { get; init; } = new();
    }
    public sealed class RequestDetails
    {
        public string Public { get; init; } = string.Empty;
        [VisibleFor("sensitive")] public string? Private { get; init; }
    }
    public sealed class RecursiveState { public RecursiveState? Next { get; init; } }
}

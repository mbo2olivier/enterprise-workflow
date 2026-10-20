using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class WorkflowModuleInfoTests
{
    [Fact]
    public void ModuleDraftInheritsRevisionButKeepsNodeIdsLocal()
    {
        var module = new WorkflowModuleInfo("sample.leave", "2.1.0", 4);
        var draft = WorkflowBuilder.Create(module, "request")
            .Start("submitted")
            .End("completed")
            .Then("submitted", "completed")
            .BuildDraft();

        Assert.Equal("sample.leave.request", draft.DefinitionId);
        Assert.Equal(4, draft.DefinitionVersion);
        Assert.Equal(["submitted", "completed"], draft.Nodes.Select(node => node.Id));
        Assert.Empty(draft.Artifacts);
    }

    [Fact]
    public void DisplayMetadataDoesNotChangeIdentityOrOriginalValue()
    {
        var original = new WorkflowModuleInfo("sample.leave", "1.0.0", 1);
        var named = original.WithName("Congés").WithDescription("Gestion des congés");

        Assert.Null(original.Name);
        Assert.Null(original.Description);
        Assert.Equal(original.Id, named.Id);
        Assert.Equal(original.ReleaseVersion, named.ReleaseVersion);
        Assert.Equal(original.ContractRevision, named.ContractRevision);
        Assert.Equal("Congés", named.Name);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0\n")]
    [InlineData("")]
    public void InvalidReleasesAreRejected(string release) =>
        Assert.Throws<ArgumentException>(() => new WorkflowModuleInfo("sample.leave", release, 1));

    [Fact]
    public void QualificationValidatesTheFinalDurableIdentifier()
    {
        var module = new WorkflowModuleInfo(new string('m', 120), "1.0.0", 1);
        Assert.Throws<ArgumentException>(() => module.Qualify("too-long"));
        Assert.Throws<ArgumentException>(() => module.Qualify("invalid/id"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowModuleInfo("sample.leave", "1.0.0", 0));
    }
}

using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Runtime;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class WorkflowValidationErrorTests
{
    [Fact]
    public void RejectionPreservesMultipleMessagesWithoutStateMutation()
    {
        var errors = new[] { new WorkflowValidationError("leave.dates", "Dates incohérentes.", "endDate"), new WorkflowValidationError("leave.balance", "Solde insuffisant.") };
        var result = HumanTaskCompletionResult.Rejected(errors);
        errors[0] = new("changed", "Changed");
        Assert.False(result.Succeeded);
        Assert.False(result.StateUpdate.ReplacesState);
        Assert.Equal(2, result.Errors.Length);
        Assert.Equal("endDate", result.Errors[0].Field);
        Assert.Equal("Dates incohérentes.", result.Errors[0].Message);
        Assert.Null(result.Errors[1].Field);
        Assert.Throws<ArgumentException>(() => HumanTaskCompletionResult.Rejected(Array.Empty<WorkflowValidationError>()));
    }

    [Fact]
    public void StateMessagesNeverGuessFormFieldsOrExposeProtectedNames()
    {
        var contract = new WorkflowStateContract(1, new() { Kind = "object", Fields = [
            new() { Name = "count", Label = "Nombre", Kind = "integer", Minimum = 2 },
            new() { Name = "secret", Label = "Secret interne", Kind = "string", Required = true, Hidden = true }] });
        var errors = contract.ValidateErrors(CanonicalJson.CreateObject("{\"count\":1}", 1024));
        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Null(error.Field));
        Assert.Contains(errors, error => error.Message.Contains("Nombre", StringComparison.Ordinal) && error.Message.Contains('2', StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Message.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(contract.ValidateErrors(CanonicalJson.CreateObject("{\"count\":\"oops\"}", 1024)));
    }
}

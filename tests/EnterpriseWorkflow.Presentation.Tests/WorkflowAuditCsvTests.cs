using EnterpriseWorkflow.Presentation;
using Xunit;

namespace EnterpriseWorkflow.Presentation.Tests;

public sealed class WorkflowAuditCsvTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData(" +1", "\"' +1\"")]
    [InlineData("@command", "\"'@command\"")]
    [InlineData("Bonjour, été", "\"Bonjour, été\"")]
    public void CsvNeutralizesFormulasAndEscapesValues(string input, string expected) => Assert.Equal(expected, WorkflowAuditService.Csv(input));
}

using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using Xunit;

namespace EnterpriseWorkflow.Presentation.Tests;

public sealed class WorkflowFormServiceTests
{
    private static readonly WorkflowFormDefinition Form = new(
        new(new("leave.request"), 3),
        "Demande de congé",
        [
            new(new("start"), "Date de début", WorkflowFormFieldKind.Date, true,
                WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant | WorkflowFieldAudience.InstanceReader),
            new(new("reason"), "Motif", WorkflowFormFieldKind.Choice, true,
                WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant,
                Options: [new("family", "Congé familial"), new("rest", "Repos")]),
            new(new("message"), "Message", WorkflowFormFieldKind.LongText, false,
                WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant,
                MaximumLength: 500),
            new(new("privateNote"), "Note privée", WorkflowFormFieldKind.ShortText, false,
                WorkflowFieldAudience.Initiator,
                MaximumLength: 100),
        ]);

    [Fact]
    public void ValidSubmissionIsCanonicalAndUnknownFieldsAreRejected()
    {
        var service = new WorkflowFormService();
        var valid = service.Validate(Form,
            "{\"reason\":\"family\",\"start\":\"2026-10-12\",\"message\":\"Bonjour\"}");

        Assert.True(valid.Succeeded);
        Assert.Equal("{\"message\":\"Bonjour\",\"reason\":\"family\",\"start\":\"2026-10-12\"}",
            valid.Submission!.CanonicalText);
        var invalid = service.Validate(Form,
            "{\"start\":\"12/10/2026\",\"reason\":\"other\",\"unexpected\":true}");
        Assert.False(invalid.Succeeded);
        Assert.Equal([
            "presentation.form-field-unknown",
            "presentation.form-field-date-expected",
            "presentation.form-field-choice-invalid",
        ], invalid.Errors.Select(error => error.Code));
    }

    [Fact]
    public void ProjectionNeverLeaksFieldsOutsideTheRequestedAudience()
    {
        var state = CanonicalJson.CreateObject(
            "{\"start\":\"2026-10-12\",\"reason\":\"family\",\"message\":\"Bonjour\",\"privateNote\":\"secret\",\"engine\":42}",
            4096);

        var projected = new WorkflowFormService().Project(Form, state, WorkflowFieldAudience.TaskParticipant);

        Assert.Equal("{\"message\":\"Bonjour\",\"reason\":\"family\",\"start\":\"2026-10-12\"}", projected.CanonicalText);
        Assert.DoesNotContain("privateNote", projected.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("engine", projected.CanonicalText, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryRejectsAProcessWhoseStartFormWasNotDeclaredFirst()
    {
        var registry = new WorkflowPresentationRegistryBuilder();

        var exception = Assert.Throws<InvalidOperationException>(() => registry.AddProcess(new(
            new TechnicalId("leave"), 1, "Congé", "Demande de congé", new(new("missing"), 1), 1,
            _ => throw new NotSupportedException())));

        Assert.Contains("must be declared before", exception.Message, StringComparison.Ordinal);
    }
}

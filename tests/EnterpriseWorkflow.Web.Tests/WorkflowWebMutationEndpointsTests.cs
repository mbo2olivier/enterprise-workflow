using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Presentation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class WorkflowWebMutationEndpointsTests
{
    [Fact]
    public void FormValuesAreConvertedToBoundedTypedJson()
    {
        var definition = new WorkflowFormDefinition(
            new(new("leave.start"), 1),
            "Congé",
            [
                new(new("start"), "Début", WorkflowFormFieldKind.Date, true, WorkflowFieldAudience.Initiator),
                new(new("days"), "Jours", WorkflowFormFieldKind.Number, true, WorkflowFieldAudience.Initiator),
                new(new("urgent"), "Urgent", WorkflowFormFieldKind.Boolean, false, WorkflowFieldAudience.Initiator),
                new(new("reason"), "Motif", WorkflowFormFieldKind.ShortText, false, WorkflowFieldAudience.Initiator),
            ]);
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["field.start"] = "2026-10-12",
            ["field.days"] = "2.5",
            ["field.urgent"] = "true",
            ["field.reason"] = "Repos",
            ["field.unexpected"] = "ignored",
        });

        var json = WorkflowWebMutationEndpoints.Serialize(definition, form);

        Assert.Equal("{\"days\":2.5,\"reason\":\"Repos\",\"start\":\"2026-10-12\",\"urgent\":true}", json);
    }

    [Fact]
    public void UncheckedBooleanIsSubmittedAsFalseAndOptionalEmptyFieldsAreOmitted()
    {
        var definition = new WorkflowFormDefinition(
            new(new("simple"), 1),
            "Simple",
            [
                new(new("accepted"), "Accepté", WorkflowFormFieldKind.Boolean, false, WorkflowFieldAudience.Initiator),
                new(new("comment"), "Commentaire", WorkflowFormFieldKind.LongText, false, WorkflowFieldAudience.Initiator),
            ]);

        var json = WorkflowWebMutationEndpoints.Serialize(definition, new FormCollection(
            new Dictionary<string, StringValues> { ["field.comment"] = "" }));

        Assert.Equal("{\"accepted\":false}", json);
    }
}

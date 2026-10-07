using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.LeaveRequest;

public sealed class LeaveRequestModule : IWorkflowModule
{
    public const string ModuleId = "sample.leave-request";
    public const string ProcessId = "leave-request";
    public const string FormId = "leave-request.form";
    public const string ApprovalNodeId = "manager-approval";

    public void ConfigureServices(IServiceCollection services) { }

    public void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers) =>
        handlers.AddHumanTask<LeaveRequestApprovalHandler>("leave-request.approval", 1);

    public void ConfigureStateMigrations(WorkflowStateMigrationRegistryBuilder migrations) { }

    public void ConfigurePresentation(WorkflowPresentationRegistryBuilder presentation)
    {
        presentation.AddForm(new(
            new(new(FormId), 1),
            "Demande de congé",
            [
                new(new("startDate"), "Date de début", WorkflowFormFieldKind.Date, true,
                    WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant | WorkflowFieldAudience.InstanceReader),
                new(new("endDate"), "Date de fin", WorkflowFormFieldKind.Date, true,
                    WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant | WorkflowFieldAudience.InstanceReader),
                new(new("leaveType"), "Type de congé", WorkflowFormFieldKind.Choice, true,
                    WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant | WorkflowFieldAudience.InstanceReader,
                    Options: [new("paid", "Congé payé"), new("family", "Congé familial"), new("unpaid", "Congé sans solde")]),
                new(new("reason"), "Motif", WorkflowFormFieldKind.LongText, true,
                    WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant,
                    MinimumLength: 3, MaximumLength: 1000,
                    HelpText: "Expliquez brièvement le motif de la demande."),
                new(new("privateNote"), "Note personnelle", WorkflowFormFieldKind.LongText, false,
                    WorkflowFieldAudience.Initiator, MaximumLength: 500),
            ],
            new("leave-request.summary"),
            "La demande est transmise au responsable explicitement désigné."));
        presentation.AddProcess(new(
            new(ProcessId),
            1,
            "Demande de congé",
            "Soumettre une absence à l’approbation d’un responsable habilité.",
            new(new(FormId), 1),
            1,
            BuildDefinition,
            [new(ApprovalNodeId)]));
    }

    private static WorkflowDefinition BuildDefinition(WorkflowArtifactReference artifact)
    {
        var compiled = WorkflowBuilder.Create(ProcessId, 1)
            .WithDisplayLabel("Demande de congé")
            .RequiresArtifact(artifact.Id.Value, artifact.Version, artifact.Sha256)
            .Start("submitted", displayLabel: "Demande soumise")
            .HumanTask(
                ApprovalNodeId,
                HumanTaskKind.Approval,
                HumanTaskAssignmentMode.DesignatedIdentity,
                FormId,
                1,
                "leave-request.approval",
                [new("task.approve", "approved"), new("task.reject", "rejected")],
                allowInitiator: false,
                displayLabel: "Décision du responsable")
            .End("closed", displayLabel: "Demande clôturée")
            .Then("submitted", ApprovalNodeId)
            .On(ApprovalNodeId, "approved", "closed")
            .On(ApprovalNodeId, "rejected", "closed")
            .Validate();
        if (!compiled.IsPublishable || compiled.Definition is null)
            throw new InvalidOperationException(string.Join("; ", compiled.Diagnostics.Select(item => item.Code)));
        return compiled.Definition;
    }
}

public sealed class LeaveRequestApprovalHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(
        HumanTaskCompletionContext context,
        CancellationToken cancellationToken)
    {
        var outcome = context.ActionId.Value switch
        {
            "task.approve" => new TechnicalId("approved"),
            "task.reject" => new TechnicalId("rejected"),
            _ => throw new InvalidOperationException("Unsupported leave-request action."),
        };
        var notification = new ExternalEffectIntent(
            new("notify-requester"),
            new("leave.notification"),
            "application/json",
            context.Submission);
        return ValueTask.FromResult(HumanTaskCompletionResult.Success(outcome, effects: [notification]));
    }
}

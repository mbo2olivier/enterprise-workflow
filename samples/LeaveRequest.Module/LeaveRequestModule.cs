using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;

namespace EnterpriseWorkflow.LeaveRequest;

/// <summary>Complete L8b example; permissions are independent of installation profile names.</summary>
public sealed class LeaveRequestModule : WorkflowModule
{
    public override WorkflowModuleInfo Info { get; } = new WorkflowModuleInfo("sample.leave-request", "1.0.0", 1)
        .WithName("Gestion des congés")
        .WithDescription("Demande, confirmation et validations RH et manager.");

    public override void ConfigurePermissions(WorkflowPermissionRegistryBuilder permissions) => permissions
        .Add("request", "Créer et confirmer une demande de congé")
        .Add("read", "Consulter les demandes de congé")
        .Add("hr.review", "Examiner une demande aux RH")
        .Add("manager.approve", "Statuer comme responsable");

    protected override void Configure(WorkflowModuleBuilder builder)
    {
        builder
            .AddHumanTask<ConfirmLeaveRequestHandler>("confirm")
            .AddService<CloseRequestHandler>("close-request")
            .AddForm("request-form", "Demande de congé", form => form
                .HandledBy<ConfirmLeaveRequestHandler>()
                .WithComponent<LeaveRequestSummary>("leave-request.summary")
                .ReadableBy(WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant | WorkflowFieldAudience.InstanceReader)
                .Fields(fields =>
                {
                    fields.Date("startDate", "Date de début").Required();
                    fields.Date("endDate", "Date de fin").Required();
                    fields.Choice("leaveType", "Type de congé").Required()
                        .Options(("paid", "Congé payé"), ("family", "Congé familial"), ("unpaid", "Congé sans solde"));
                    fields.LongText("reason", "Motif").Required().Length(3, 1000)
                        .ReadableBy(WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant)
                        .Help("Expliquez brièvement le motif de la demande.");
                    fields.LongText("privateNote", "Note personnelle").Length(0, 500)
                        .ReadableBy(WorkflowFieldAudience.Initiator);
                }))
            .AddProcess<LeaveRequestProcess>();
    }
}

public sealed class LeaveRequestProcess : IWorkflowProcess
{
    public void Configure(WorkflowProcessBuilder process)
    {
        process.WithId("leave-request")
            .WithName("Demande de congé")
            .WithDescription("Confirmer sa demande, puis la soumettre aux RH et au responsable choisi.")
            .WithStateSchemaVersion(1)
            .RequiresPermission("request")
            .ReadableByPermission("read")
            .StartWithForm("request-form").Then("draft");

        // A business confirmation stage after creation, not a framework draft feature.
        process.Input("draft", "Confirmation du demandeur", task => task
            .RequiresPermission("request")
            .AssignedToInitiator()
            .HandledBy<ConfirmLeaveRequestHandler>()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("submit", "Confirmer", action => action.Then("validation-rh"));
                actions.Add("cancel", "Annuler", action => action.Then("cancelled"));
            }));

        process.Approval("validation-rh", "Validation RH", task => task
            .RequiresPermission("hr.review")
            .ClaimedByEligibleParticipant()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("validate", "Valider et désigner le responsable", action => action
                    .SelectAssigneeFor("validation-manager").Then("validation-manager"));
                actions.Add("decline", "Refuser", action => action.RequireComment().Then("rejected"));
            }));

        process.Approval("validation-manager", "Validation du responsable", task => task
            .RequiresPermission("manager.approve")
            .AssignedFromPreviousTask()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("validate", "Approuver", action => action.Then("close-request"));
                actions.Add("decline", "Refuser", action => action.RequireComment().Then("rejected"));
            }));

        process.Service<CloseRequestHandler>("close-request").Then("completed");
        process.End("completed").End("rejected").End("cancelled");
    }
}

public sealed class ConfirmLeaveRequestHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(HumanTaskCompletionResult.Success(context.ActionId));
}

public sealed class CloseRequestHandler : IServiceNodeHandler
{
    public ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ServiceNodeResult.Success());
}

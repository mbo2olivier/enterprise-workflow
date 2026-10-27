using EnterpriseWorkflow.Core.Serialization;
using EnterpriseWorkflow.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;

namespace EnterpriseWorkflow.LeaveRequest;

/// <summary>Complete L8b example; permissions are independent of installation profile names.</summary>
public sealed class LeaveRequestModule : WorkflowModule
{
    public override WorkflowModuleInfo Info { get; } = new WorkflowModuleInfo("sample.leave-request", "1.6.0", 7)
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
            .MediaPolicies(policies =>
            {
                policies.Add("supporting-documents").Label("Justificatifs")
                    .AllowFormats(Enum.GetValues<MediaFormats>())
                    .MaximumSize(10.Megabytes()).VisibleToInstanceReaders();
                policies.Add("generated-documents").Label("Documents générés")
                    .AllowFormats(MediaFormats.Text, MediaFormats.Pdf).MaximumSize(1.Megabytes()).VisibleToInstanceReaders();
            })
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
                    fields.File("documents", "Justificatifs").UseMediaPolicy("supporting-documents").MaximumFiles(3);
                    fields.LongText("privateNote", "Note personnelle").Length(0, 500)
                        .ReadableBy(WorkflowFieldAudience.Initiator);
                }))
            .AddForm("confirmation-form", "Confirmation du demandeur", form => form
                .HandledBy<ConfirmLeaveRequestHandler>()
                .WithComponent<LeaveRequestSummary>("leave-confirmation.summary")
                .Fields(fields => fields.LongText("comment", "Commentaire facultatif")))
            .AddProcess<LeaveRequestProcess>();
    }
}

public sealed class LeaveRequestProcess : IWorkflowProcess
{
    public void Configure(WorkflowProcessBuilder process)
    {
        var flow = process.WithState<LeaveRequestState>();
        flow.WithId("leave-request")
            .WithName("Demande de congé")
            .WithDescription("Confirmer sa demande, puis la soumettre aux RH et au responsable choisi.")
            .InitializedBy<LeaveRequestInitializer>()
            .WithStateSchemaVersion(4)
            .RequiresPermission("request")
            .ReadableByPermission("read")
            .StartWithForm("request-form").Then("draft");

        // A business confirmation stage after creation, not a framework draft feature.
        flow.Input("draft", "Confirmation du demandeur", task => task
            .UsesForm("confirmation-form")
            .RequiresPermission("request")
            .AssignedToInitiator()
            .HandledBy<ConfirmLeaveRequestHandler>()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("submit", "Confirmer", action => action.Then("validation-rh"));
                actions.Add("cancel", "Annuler", action => action.Then("cancelled"));
            }));

        flow.Approval("validation-rh", "Validation RH", task => task
            .RequiresPermission("hr.review")
            .ClaimedByEligibleParticipant()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("validate", "Valider et désigner le responsable", action => action
                    .CommentAt(state => state.Comments).SelectAssigneeFor("validation-manager").Then("validation-manager"));
                actions.Add("decline", "Refuser", action => action.RequireCommentAt(state => state.Comments).Then("rejected"));
            }));

        flow.Approval("validation-manager", "Validation du responsable", task => task
            .RequiresPermission("manager.approve")
            .AssignedFromPreviousTask()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("validate", "Approuver", action => action.CommentAt(state => state.Comments).Then("close-request"));
                actions.Add("decline", "Refuser", action => action.RequireCommentAt(state => state.Comments).Then("rejected"));
            }));

        flow.Service<CloseRequestHandler>("close-request").Then("completed");
        flow.End("completed", WorkflowCompletionOutcome.Succeeded, "Demande acceptée")
            .End("rejected", WorkflowCompletionOutcome.Declined, "Demande refusée")
            .End("cancelled", WorkflowCompletionOutcome.Cancelled, "Demande annulée");
    }
}

public sealed class ConfirmLeaveRequestHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(HumanTaskCompletionResult.Success());
}

public sealed class CloseRequestHandler(IWorkflowMediaService media) : IServiceNodeHandler
{
    public async ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var state = context.ReadState<LeaveRequestState>();
        state.Closed = true;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"Demande {context.InstanceId.Value} : validations terminées ({state.StartDate} au {state.EndDate})."));
        await media.For(context).StoreAsync("closure-summary", "cloture.txt", stream, "generated-documents", cancellationToken);
        return ServiceNodeResult.Success(NodeStateUpdate.Replace(state));
    }
}

[StateValidator(typeof(LeaveDatesValidator))]
public sealed class LeaveRequestState
{
    [JsonPropertyName("closed"), Label("Clôturée")] public bool Closed { get; set; }
    [CommentState, JsonPropertyName("comments"), Label("Commentaires")] public List<WorkflowComment> Comments { get; init; } = [];
    [JsonPropertyName("startDate"), Label("Date de début")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("endDate"), Label("Date de fin")] public DateOnly EndDate { get; init; }
    [JsonPropertyName("leaveType"), Label("Type de congé"), Required] public string LeaveType { get; init; } = string.Empty;
    [JsonPropertyName("reason"), Label("Motif"), Required, StringLength(1000, MinimumLength = 3)] public string Reason { get; init; } = string.Empty;
    [JsonPropertyName("privateNote"), HiddenField] public string? PrivateNote { get; init; }
}

public sealed class LeaveRequestForm
{
    [JsonPropertyName("startDate")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("endDate")] public DateOnly EndDate { get; init; }
    [JsonPropertyName("leaveType")] public string LeaveType { get; init; } = "";
    [JsonPropertyName("reason")] public string Reason { get; init; } = "";
    [JsonPropertyName("privateNote")] public string? PrivateNote { get; init; }
    [JsonPropertyName("documents")] public List<WorkflowMediaReference> Documents { get; init; } = [];
}

public sealed class LeaveRequestInitializer(IWorkflowMediaService media, IWorkflowSerializer serializer) : IWorkflowStateInitializer
{
    public async ValueTask<WorkflowStateInitializationResult> InitializeAsync(WorkflowStateInitializationContext context, CancellationToken cancellationToken)
    {
        await media.For(context).FromFieldAsync("documents", cancellationToken);
        var input = context.ReadSubmission<LeaveRequestForm>();
        var state = new LeaveRequestState
        {
            StartDate = input.StartDate, EndDate = input.EndDate,
            LeaveType = input.LeaveType, Reason = input.Reason,
            PrivateNote = input.PrivateNote,
        };
        return new WorkflowStateInitializationResult(serializer.Serialize(state));
    }
}
public sealed class LeaveDatesValidator : IWorkflowStateValidator
{
    public ValueTask<IReadOnlyList<string>> ValidateAsync(WorkflowStateValidationContext context, CancellationToken cancellationToken)
    {
        var state = context.State.Value.ToJsonElement();
        IReadOnlyList<string> errors = state.GetProperty("endDate").Deserialize<DateOnly>() < state.GetProperty("startDate").Deserialize<DateOnly>() ? ["endDate"] : [];
        return ValueTask.FromResult(errors);
    }
}

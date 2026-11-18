using static EnterpriseWorkflow.Sdk.WorkflowTexts;
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
    public override WorkflowModuleInfo Info { get; } = new WorkflowModuleInfo("sample.leave-request", "1.9.0", 10)
        .WithName("Gestion des congés")
        .WithDescription("Demande, confirmation, approbation hiérarchique puis validation RH.");

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
            .AddForm("request-form", Text("process.title", "Leave request"), form => form
                .HandledBy<ConfirmLeaveRequestHandler>()
                .WithComponent<LeaveRequestSummary>("leave-request.summary")
                .ReadableBy(WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant | WorkflowFieldAudience.InstanceReader)
                .Fields(fields =>
                {
                    fields.Date("startDate", Text("field.startDate", "Start date")).Required();
                    fields.Date("endDate", Text("field.endDate", "End date")).Required();
                    fields.Choice("leaveType", Text("field.leaveType", "Leave type")).Required()
                        .Options(("paid", Text("choice.paid", "Paid leave")), ("family", Text("choice.family", "Family leave")), ("unpaid", Text("choice.unpaid", "Unpaid leave")));
                    fields.LongText("reason", Text("field.reason", "Reason")).Required().Length(3, 1000)
                        .ReadableBy(WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant)
                        .Help(Text("help.reason", "Briefly explain the reason for your request."));
                    fields.File("documents", Text("field.documents", "Supporting documents")).UseMediaPolicy("supporting-documents").MaximumFiles(3);
                    fields.LongText("privateNote", Text("field.privateNote", "Personal note")).Length(0, 500)
                        .ReadableBy(WorkflowFieldAudience.Initiator);
                }))
            .AddForm("confirmation-form", Text("form.confirmation", "Requester confirmation"), form => form
                .HandledBy<ConfirmLeaveRequestHandler>()
                .WithComponent<LeaveRequestSummary>("leave-confirmation.summary")
                .Fields(fields => fields.LongText("comment", Text("field.comment", "Optional comment"))))
            .AddProcess<LeaveRequestProcess>();
    }
}

public sealed class LeaveRequestProcess : IWorkflowProcess
{
    public void Configure(WorkflowProcessBuilder process)
    {
        var flow = process.WithState<LeaveRequestState>();
        flow.WithId("leave-request")
            .WithName(Text("process.title", "Leave request"))
            .WithDescription(Text("process.description", "Confirm the request, then submit it to the selected manager before HR review."))
            .InitializedBy<LeaveRequestInitializer>()
            .WithStateSchemaVersion(4)
            .RequiresPermission("request")
            .ReadableByPermission("read")
            .StartWithForm("request-form").Then("draft");

        // A business confirmation stage after creation, not a framework draft feature.
        flow.Input("draft", Text("form.confirmation", "Requester confirmation"), task => task
            .UsesForm("confirmation-form")
            .RequiresPermission("request")
            .AssignedToInitiator()
            .HandledBy<ConfirmLeaveRequestHandler>()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("submit", Text("action.submit", "Confirm"), action => action.SelectAssigneeFor("validation-manager").Then("validation-manager"));
                actions.Add("cancel", Text("action.cancel", "Cancel"), action => action.Then("cancelled"));
            }));

        flow.Approval("validation-manager", Text("stage.manager", "Manager approval"), task => task
            .RequiresPermission("manager.approve")
            .AssignedFromPreviousTask()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("validate", Text("action.approve", "Approve"), action => action.CommentAt(state => state.Comments).Then("validation-rh"));
                actions.Add("decline", Text("action.decline", "Decline"), action => action.RequireCommentAt(state => state.Comments).Then("rejected"));
            }));

        flow.Approval("validation-rh", Text("stage.hr", "HR approval"), task => task
            .RequiresPermission("hr.review")
            .ClaimedByEligibleParticipant()
            .ShowFields("startDate", "endDate", "leaveType", "reason")
            .Actions(actions =>
            {
                actions.Add("validate", Text("action.validate", "Validate"), action => action.CommentAt(state => state.Comments).Then("close-request"));
                actions.Add("decline", Text("action.decline", "Decline"), action => action.RequireCommentAt(state => state.Comments).Then("rejected"));
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
    [JsonPropertyName("closed"), LabelKey("field.closed", "Closed")] public bool Closed { get; set; }
    [CommentState, JsonPropertyName("comments"), LabelKey("field.comments", "Comments")] public List<WorkflowComment> Comments { get; init; } = [];
    [JsonPropertyName("startDate"), LabelKey("field.startDate", "Start date")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("endDate"), LabelKey("field.endDate", "End date")] public DateOnly EndDate { get; init; }
    [JsonPropertyName("leaveType"), LabelKey("field.leaveType", "Leave type"), Required] public string LeaveType { get; init; } = string.Empty;
    [JsonPropertyName("reason"), LabelKey("field.reason", "Reason"), Required, StringLength(1000, MinimumLength = 3)] public string Reason { get; init; } = string.Empty;
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
    public ValueTask<IReadOnlyList<WorkflowValidationError>> ValidateErrorsAsync(WorkflowStateValidationContext context, CancellationToken cancellationToken)
    {
        var state = context.State.Value.ToJsonElement();
        IReadOnlyList<WorkflowValidationError> errors = state.GetProperty("endDate").Deserialize<DateOnly>() < state.GetProperty("startDate").Deserialize<DateOnly>() ? [new("leave.invalid-dates", Text("validation.dates", "End date must be on or after start date."))] : [];
        return ValueTask.FromResult(errors);
    }
}

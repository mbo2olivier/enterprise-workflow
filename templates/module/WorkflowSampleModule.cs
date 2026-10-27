using EnterpriseWorkflow.Core.Serialization;
using EnterpriseWorkflow.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;

namespace WorkflowModuleProject;

public sealed class Module : WorkflowModule
{
    public override WorkflowModuleInfo Info { get; } = new WorkflowModuleInfo("sample.workflow", "1.0.0", 1)
        .WithName("WorkflowSample");

    public override void ConfigurePermissions(WorkflowPermissionRegistryBuilder permissions) => permissions
        .Add("request", "Créer une demande")
        .Add("review", "Examiner une demande")
        .Add("read", "Consulter les demandes");

    protected override void Configure(WorkflowModuleBuilder builder) => builder
        .AddHumanTask<ConfirmHandler>("confirm")
        .AddService<CloseHandler>("close")
        .AddForm("request-form", "Nouvelle demande", form => form
            .HandledBy<ConfirmHandler>()
            .ReadableBy(WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant | WorkflowFieldAudience.InstanceReader)
            .Fields(fields => fields.Text("reason", "Motif").Required().Length(3, 200)))
        .AddProcess<RequestProcess>();
}

public sealed class RequestProcess : IWorkflowProcess
{
    public void Configure(WorkflowProcessBuilder process)
    {
        var flow = process
            .WithId("request")
            .WithName("Nouvelle demande")
            .WithState<RequestState>().InitializedBy<RequestInitializer>()
            .WithStateSchemaVersion(1)
            ;
        flow
            .RequiresPermission("request")
            .ReadableByPermission("read")
            ;
        flow.
            StartWithForm("request-form")
            .Then("review");

        flow
            .Approval("review", "Validation", task => task.RequiresPermission("review")
            .ClaimedByEligibleParticipant().ShowFields("reason").Actions(actions =>
            {
                actions.Add("approve", "Approuver", action => action.CommentAt(state => state.Comments).Then("close"));
                actions.Add("decline", "Refuser", action => action.RequireCommentAt(state => state.Comments).Then("rejected"));
            }));

        flow
            .Service<CloseHandler>("close")
            .Then("completed");
        flow.End("completed", WorkflowCompletionOutcome.Succeeded, "Acceptée").End("rejected", WorkflowCompletionOutcome.Declined, "Refusée");
    }
}

public sealed class ConfirmHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(HumanTaskCompletionResult.Success());
}
public sealed class CloseHandler : IServiceNodeHandler
{
    public ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var state = context.ReadState<RequestState>();
        state.Closed = true;
        return ValueTask.FromResult(ServiceNodeResult.Success(NodeStateUpdate.Replace(state)));
    }
}

public sealed class RequestState
{
    [JsonPropertyName("closed"), Label("Clôturée")] public bool Closed { get; set; }
    [CommentState, JsonPropertyName("comments"), Label("Commentaires")] public List<WorkflowComment> Comments { get; init; } = [];
    [JsonPropertyName("reason"), Label("Motif"), Required, StringLength(200, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}
public sealed class RequestForm
{
    [JsonPropertyName("reason")] public string Reason { get; init; } = "";
}
public sealed class RequestInitializer(IWorkflowSerializer serializer) : IWorkflowStateInitializer
{
    public ValueTask<WorkflowStateInitializationResult> InitializeAsync(WorkflowStateInitializationContext context, CancellationToken cancellationToken)
    {
        var input = context.ReadSubmission<RequestForm>();
        var state = new RequestState { Reason = input.Reason };
        return ValueTask.FromResult(new WorkflowStateInitializationResult(serializer.Serialize(state)));
    }
}

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
        process
            .WithId("request")
            .WithName("Nouvelle demande")
            .WithStateSchemaVersion(1)
            ;
        process
            .RequiresPermission("request")
            .ReadableByPermission("read")
            ;
        process.
            StartWithForm("request-form")
            .Then("review");

        process
            .Approval("review", "Validation", task => task.RequiresPermission("review")
            .ClaimedByEligibleParticipant().ShowFields("reason").Actions(actions =>
            {
                actions.Add("approve", "Approuver", action => action.Then("close"));
                actions.Add("decline", "Refuser", action => action.RequireComment().Then("rejected"));
            }));

        process
            .Service<CloseHandler>("close")
            .Then("completed");
        process.End("completed").End("rejected");
    }
}

public sealed class ConfirmHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(HumanTaskCompletionResult.Success(context.ActionId));
}
public sealed class CloseHandler : IServiceNodeHandler
{
    public ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ServiceNodeResult.Success());
}

using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Fixtures.ApprovalModule;

public sealed class ApprovalModule : IWorkflowModule
{
    public void ConfigureServices(IServiceCollection services) { }

    public void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers) =>
        handlers.AddHumanTask<ApprovalHandler>("fixture.approval", 1);
}

public sealed class ApprovalHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(
        HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(HumanTaskCompletionResult.Success(new TechnicalId("approved")));
}

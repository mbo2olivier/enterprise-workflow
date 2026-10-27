using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Fixtures.ApprovalModule;

public sealed class ApprovalModule : IWorkflowModule
{
    public void ConfigureServices(IServiceCollection services) { }

    public void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers) =>
        handlers.AddHumanTask<ApprovalHandler>("fixture.approval", 2);

    public void ConfigureStateMigrations(WorkflowStateMigrationRegistryBuilder migrations) { }

    public void ConfigurePresentation(WorkflowPresentationRegistryBuilder presentation) =>
        presentation.MediaFormats.Add(new(new("fixture.approval.note", "application/x-note", [".note"]), typeof(NoteValidator)));
}

public sealed class ApprovalHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(
        HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(HumanTaskCompletionResult.Success());
}

public sealed class NoteValidator : IWorkflowMediaFormatValidator
{
    public async ValueTask<bool> ValidateAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        return (await reader.ReadToEndAsync(cancellationToken)).StartsWith("V2:", StringComparison.Ordinal);
    }
}

using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Fixtures.ApprovalModule;

public sealed class ApprovalModule : IWorkflowModule
{
    public void ConfigureServices(IServiceCollection services) { }

    public void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers) =>
        handlers.AddHumanTask<ApprovalHandler>("fixture.approval", 1);

    public void ConfigureStateMigrations(WorkflowStateMigrationRegistryBuilder migrations) =>
        migrations.Add<ApprovalStateMigrator>(1, 2).Add<FailingStateMigrator>(2, 3);

    public void ConfigurePresentation(WorkflowPresentationRegistryBuilder presentation) =>
        presentation.MediaFormats.Add(new(new("fixture.approval.note", "application/x-note", [".note"]), typeof(NoteValidator)));
}

public sealed class FailingStateMigrator : IWorkflowStateMigrator
{
    public ValueTask<WorkflowState> MigrateAsync(WorkflowState source, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Fixture migration failure.");
}

public sealed class ApprovalStateMigrator : IWorkflowStateMigrator
{
    public ValueTask<WorkflowState> MigrateAsync(WorkflowState source, CancellationToken cancellationToken) =>
        ValueTask.FromResult(WorkflowState.Create("{\"migrated\":true}", 2));
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
        return (await reader.ReadToEndAsync(cancellationToken)).StartsWith("V1:", StringComparison.Ordinal);
    }
}

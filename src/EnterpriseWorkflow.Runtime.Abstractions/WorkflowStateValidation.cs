using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Pure cross-field state validation; never perform external effects here.</summary>
public interface IWorkflowStateValidator
{
    ValueTask<IReadOnlyList<string>> ValidateAsync(WorkflowStateValidationContext context, CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
    async ValueTask<IReadOnlyList<WorkflowValidationError>> ValidateErrorsAsync(WorkflowStateValidationContext context, CancellationToken cancellationToken) =>
        (await ValidateAsync(context, cancellationToken).ConfigureAwait(false)).Take(50).Select(_ => new WorkflowValidationError("runtime.state-invalid", "L’état de la demande ne respecte pas les règles du processus.")).ToArray();
}
public sealed record WorkflowStateValidationContext(WorkflowState State, string? NodeId, string? ActionId, IReadOnlyList<string> Groups);
public interface IWorkflowStateValidationService
{
    WorkflowStateContract? GetContract(WorkflowDefinition definition, int schemaVersion);
    ValueTask<IReadOnlyList<WorkflowValidationError>> ValidateErrorsAsync(WorkflowDefinition definition, WorkflowStateValidationContext context, CancellationToken cancellationToken);
    async ValueTask<bool> ValidateAsync(WorkflowDefinition definition, WorkflowStateValidationContext context, CancellationToken cancellationToken) =>
        (await ValidateErrorsAsync(definition, context, cancellationToken).ConfigureAwait(false)).Count == 0;
}

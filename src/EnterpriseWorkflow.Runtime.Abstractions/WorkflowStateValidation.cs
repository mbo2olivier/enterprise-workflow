using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Pure cross-field state validation; never perform external effects here.</summary>
public interface IWorkflowStateValidator
{
    ValueTask<IReadOnlyList<string>> ValidateAsync(WorkflowStateValidationContext context, CancellationToken cancellationToken);
}
public sealed record WorkflowStateValidationContext(WorkflowState State, string? NodeId, string? ActionId, IReadOnlyList<string> Groups);
public interface IWorkflowStateValidationService
{
    WorkflowStateContract? GetContract(WorkflowDefinition definition, int schemaVersion);
    ValueTask<bool> ValidateAsync(WorkflowDefinition definition, WorkflowStateValidationContext context, CancellationToken cancellationToken);
}

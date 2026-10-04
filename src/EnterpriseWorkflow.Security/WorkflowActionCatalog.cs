namespace EnterpriseWorkflow.Security;

public sealed class WorkflowActionCatalog(IEnumerable<WorkflowActionDescriptor> descriptors) : IWorkflowActionCatalog
{
    private readonly HashSet<WorkflowAuthorizationScope> _scopes = descriptors
        .Select(descriptor => descriptor.Scope ?? throw new ArgumentException("A workflow action descriptor requires a scope.", nameof(descriptors)))
        .ToHashSet();

    public ValueTask<bool> ContainsAsync(WorkflowAuthorizationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_scopes.Contains(scope));
    }
}

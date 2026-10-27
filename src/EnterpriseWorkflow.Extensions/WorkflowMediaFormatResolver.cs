using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Extensions;

internal sealed class WorkflowMediaFormatResolver(IWorkflowPresentationCatalog catalog, IServiceScopeFactory scopes) : IWorkflowMediaFormatResolver
{
    public async ValueTask<bool> ValidateAsync(WorkflowArtifactReference artifact, WorkflowMediaFormatDefinition format, Stream content, CancellationToken cancellationToken)
    {
        if (!catalog.TryResolveMediaFormat(artifact, format.Id, out var type) || type is null) return false;
        await using var scope = scopes.CreateAsyncScope();
        var validator = (IWorkflowMediaFormatValidator)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, type);
        return await validator.ValidateAsync(content, cancellationToken).ConfigureAwait(false);
    }
}

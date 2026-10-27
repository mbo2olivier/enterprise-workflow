using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Trusted bounded format validator. Return false or throw to reject; never execute the document.</summary>
public interface IWorkflowMediaFormatValidator
{
    ValueTask<bool> ValidateAsync(Stream content, CancellationToken cancellationToken);
}

public interface IWorkflowMediaFormatResolver
{
    ValueTask<bool> ValidateAsync(WorkflowArtifactReference artifact, WorkflowMediaFormatDefinition format, Stream content, CancellationToken cancellationToken);
}

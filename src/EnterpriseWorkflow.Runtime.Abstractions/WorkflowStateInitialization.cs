using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Explicit mapping from independently validated start-form data to business state.</summary>
public interface IWorkflowStateInitializer
{
    ValueTask<WorkflowStateInitializationResult> InitializeAsync(WorkflowStateInitializationContext context, CancellationToken cancellationToken);
}

public sealed record WorkflowStateInitializationContext(ActorIdentity Initiator, CanonicalJson Submission, string SubmissionKey)
{
    public TInput ReadSubmission<TInput>() where TInput : class => Submission.Deserialize<TInput>();
    public WorkflowMediaSession? MediaExecution { get; init; }
}

public sealed record WorkflowStateInitializationResult(CanonicalJson State);

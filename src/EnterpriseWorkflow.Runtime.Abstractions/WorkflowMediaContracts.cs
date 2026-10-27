using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

public sealed record WorkflowMediaSpecification(long MaximumBytes, string[] AllowedContentTypes, string[] AllowedExtensions, WorkflowMediaFormatDefinition[]? Formats = null);
public sealed record WorkflowMediaVisibility(bool Hidden, string[][] PermissionGroups);
/// <summary>Trusted module context. HTTP adapters must derive this scope server-side, never bind it from JSON.</summary>
public sealed record WorkflowMediaWriteScope(ActorIdentity Owner, WorkflowInstanceId? InstanceId,
    NodeActivationId? ActivationId, string? SubmissionKey = null, string? SourceField = null, WorkflowArtifactReference? Artifact = null);

public interface IWorkflowMediaStorageProvider
{
    ValueTask WriteAsync(string objectKey, Stream source, long maximumBytes, CancellationToken cancellationToken);
    ValueTask<Stream> OpenAsync(string objectKey, CancellationToken cancellationToken);
    ValueTask DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public interface IWorkflowMediaScanner
{
    /// <summary>Return false to block the file; unavailable scanners must throw, never silently allow.</summary>
    ValueTask<bool> ApproveAsync(Stream content, string detectedContentType, CancellationToken cancellationToken);
}

public enum WorkflowMediaInspectionDecision { Approved, Pending, Rejected }
public interface IWorkflowMediaInspector
{
    ValueTask<WorkflowMediaInspectionDecision> InspectAsync(Stream content, WorkflowMediaRecord media, CancellationToken cancellationToken);
}

public interface IWorkflowMediaService
{
    ValueTask<WorkflowMediaRecord?> GetStagedAsync(WorkflowMediaReference reference, CancellationToken cancellationToken) => ValueTask.FromResult<WorkflowMediaRecord?>(null);
    /// <summary>Trusted scanner completion, bound to the immutable hash. No public callback endpoint is provided.</summary>
    ValueTask<bool> CompleteInspectionAsync(WorkflowMediaReference reference, string expectedSha256, bool approved, CancellationToken cancellationToken);
    ValueTask<int> PurgeExpiredStagingAsync(TimeSpan minimumAge, int batchSize, CancellationToken cancellationToken);
    ValueTask<WorkflowMediaRecord> StageAsync(WorkflowMediaWriteScope scope, string operationId, string fileName,
        Stream content, WorkflowMediaSpecification specification, WorkflowMediaVisibility visibility,
        CancellationToken cancellationToken, WorkflowMediaReference? replace = null);
    ValueTask<WorkflowMediaRecord?> GetForInstanceAsync(WorkflowInstanceId instanceId, WorkflowMediaReference reference, CancellationToken cancellationToken);
    ValueTask<bool> DeleteForInstanceAsync(WorkflowInstanceId instanceId, WorkflowMediaReference reference, CancellationToken cancellationToken);
    ValueTask<Stream> OpenForInstanceAsync(WorkflowInstanceId instanceId, WorkflowMediaReference reference, CancellationToken cancellationToken);
}

public static class WorkflowMediaContextExtensions
{
    public static WorkflowMediaSession For(this IWorkflowMediaService service, NodeExecutionContext context) => (context.MediaExecution ?? throw new InvalidOperationException("No runtime media context.")).Bind(service);
    public static WorkflowMediaSession For(this IWorkflowMediaService service, HumanTaskCompletionContext context) => (context.MediaExecution ?? throw new InvalidOperationException("No runtime media context.")).Bind(service);
    public static WorkflowMediaSession For(this IWorkflowMediaService service, WorkflowStateInitializationContext context) => (context.MediaExecution ?? throw new InvalidOperationException("No runtime media context.")).Bind(service);
}

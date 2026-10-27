namespace EnterpriseWorkflow.Persistence;

public sealed record WorkflowMediaReference(Guid Id, int Revision);
public enum WorkflowMediaStatus { Staged, Active, Deleted, Superseded, Abandoned, Quarantined }
/// <summary>Private metadata. Authorization must precede disclosure of object keys or file contents.</summary>
public sealed record WorkflowMediaRecord(WorkflowMediaReference Reference, string Name, string ContentType, long Length,
    string Sha256, string ObjectKey, ActorIdentity Owner, WorkflowInstanceId? InstanceId, NodeActivationId? ActivationId,
    string? SubmissionKey, string? SourceField, bool Hidden, string[][] PermissionGroups, WorkflowMediaStatus Status,
    DateTimeOffset CreatedAtUtc);
public sealed record StageWorkflowMediaCommand(WorkflowMediaRecord Media);
public sealed record DeleteWorkflowMediaCommand(WorkflowMediaReference Reference, WorkflowInstanceId InstanceId);

public sealed class WorkflowMediaCommitException : InvalidOperationException
{
    public WorkflowMediaCommitException() : base("Media attachment scope or revision is invalid.") { }
}

public sealed record CompleteWorkflowMediaInspectionCommand(WorkflowMediaReference Reference, string ExpectedSha256, bool Approved);

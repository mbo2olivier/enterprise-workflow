namespace Example.Workflow;

public interface IApprovalPolicy
{
    Task<bool> CanApproveAsync(string reason, CancellationToken cancellationToken);
}

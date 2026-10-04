using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Bounded, immutable input supplied to one node-handler attempt.</summary>
public sealed class NodeExecutionContext
{
    /// <summary>Creates a detached execution context.</summary>
    public NodeExecutionContext(
        WorkflowInstanceId instanceId,
        NodeActivationId activationId,
        TechnicalId nodeId,
        int attempt,
        WorkflowState state,
        CanonicalJson configuration)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempt);
        if (instanceId.Value == Guid.Empty)
        {
            throw new ArgumentException("The workflow instance identifier cannot be empty.", nameof(instanceId));
        }

        if (activationId.Value == Guid.Empty)
        {
            throw new ArgumentException("The node activation identifier cannot be empty.", nameof(activationId));
        }

        if (!TechnicalId.IsValid(nodeId.Value))
        {
            throw new ArgumentException("The node identifier is invalid.", nameof(nodeId));
        }

        InstanceId = instanceId;
        ActivationId = activationId;
        NodeId = nodeId;
        Attempt = attempt;
        State = state ?? throw new ArgumentNullException(nameof(state));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>Gets the durable workflow instance.</summary>
    public WorkflowInstanceId InstanceId { get; }

    /// <summary>Gets the logical activation, stable across retry attempts.</summary>
    public NodeActivationId ActivationId { get; }

    /// <summary>Gets the node being executed.</summary>
    public TechnicalId NodeId { get; }

    /// <summary>Gets the positive attempt number for diagnostics only.</summary>
    public int Attempt { get; }

    /// <summary>Gets the immutable complete state observed for this attempt.</summary>
    public WorkflowState State { get; }

    /// <summary>Gets the validated, canonical node configuration.</summary>
    public CanonicalJson Configuration { get; }
}

/// <summary>Detached durable input supplied to one human-task completion handler.</summary>
public sealed record HumanTaskCompletionContext(
    HumanTaskId TaskId,
    WorkflowInstanceId InstanceId,
    NodeActivationId ActivationId,
    TechnicalId NodeId,
    ActorIdentity Actor,
    TechnicalId ActionId,
    CanonicalJson Submission,
    WorkflowState State,
    CanonicalJson Configuration);

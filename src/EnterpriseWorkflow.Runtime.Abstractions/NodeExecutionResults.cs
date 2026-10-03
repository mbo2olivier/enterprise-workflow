using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Bounded outcome categories returned by executable handlers.</summary>
public enum NodeExecutionDisposition
{
    /// <summary>The business operation completed successfully.</summary>
    Succeeded,

    /// <summary>The operation did not complete and the worker may apply its retry policy.</summary>
    RetryableFailure,

    /// <summary>The operation failed permanently.</summary>
    PermanentFailure,
}

/// <summary>Result returned by a service-node handler.</summary>
public sealed class ServiceNodeResult
{
    private ServiceNodeResult(
        NodeExecutionDisposition disposition,
        NodeStateUpdate stateUpdate,
        TechnicalId? errorCode)
    {
        Disposition = disposition;
        StateUpdate = stateUpdate;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the bounded outcome category.</summary>
    public NodeExecutionDisposition Disposition { get; }

    /// <summary>Gets the successful state update, or <see cref="NodeStateUpdate.Unchanged"/> on failure.</summary>
    public NodeStateUpdate StateUpdate { get; }

    /// <summary>Gets the qualified machine-readable failure code.</summary>
    public TechnicalId? ErrorCode { get; }

    /// <summary>Creates a successful result. The runtime chooses the next node.</summary>
    public static ServiceNodeResult Success(NodeStateUpdate? stateUpdate = null) =>
        new(NodeExecutionDisposition.Succeeded, stateUpdate ?? NodeStateUpdate.Unchanged, null);

    /// <summary>Creates a retryable failure. The worker owns delay and retry limits.</summary>
    public static ServiceNodeResult Retryable(TechnicalId errorCode) =>
        new(NodeExecutionDisposition.RetryableFailure, NodeStateUpdate.Unchanged, Validate(errorCode));

    /// <summary>Creates a permanent failure.</summary>
    public static ServiceNodeResult PermanentFailure(TechnicalId errorCode) =>
        new(NodeExecutionDisposition.PermanentFailure, NodeStateUpdate.Unchanged, Validate(errorCode));

    private static TechnicalId Validate(TechnicalId errorCode) => TechnicalId.IsValid(errorCode.Value)
        ? errorCode
        : throw new ArgumentException("The error code is invalid.", nameof(errorCode));
}

/// <summary>Result returned by an exclusive-decision handler.</summary>
public sealed class DecisionNodeResult
{
    private DecisionNodeResult(
        NodeExecutionDisposition disposition,
        TechnicalId? outcome,
        NodeStateUpdate stateUpdate,
        TechnicalId? errorCode)
    {
        Disposition = disposition;
        Outcome = outcome;
        StateUpdate = stateUpdate;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the bounded outcome category.</summary>
    public NodeExecutionDisposition Disposition { get; }

    /// <summary>Gets the selected declared outcome on success.</summary>
    public TechnicalId? Outcome { get; }

    /// <summary>Gets the successful state update, or <see cref="NodeStateUpdate.Unchanged"/> on failure.</summary>
    public NodeStateUpdate StateUpdate { get; }

    /// <summary>Gets the qualified machine-readable failure code.</summary>
    public TechnicalId? ErrorCode { get; }

    /// <summary>Creates a successful decision. The runtime validates and follows the declared outcome.</summary>
    public static DecisionNodeResult Select(TechnicalId outcome, NodeStateUpdate? stateUpdate = null) =>
        new(NodeExecutionDisposition.Succeeded, Validate(outcome, nameof(outcome)), stateUpdate ?? NodeStateUpdate.Unchanged, null);

    /// <summary>Creates a retryable failure. The worker owns delay and retry limits.</summary>
    public static DecisionNodeResult Retryable(TechnicalId errorCode) =>
        new(NodeExecutionDisposition.RetryableFailure, null, NodeStateUpdate.Unchanged, Validate(errorCode, nameof(errorCode)));

    /// <summary>Creates a permanent failure.</summary>
    public static DecisionNodeResult PermanentFailure(TechnicalId errorCode) =>
        new(NodeExecutionDisposition.PermanentFailure, null, NodeStateUpdate.Unchanged, Validate(errorCode, nameof(errorCode)));

    private static TechnicalId Validate(TechnicalId value, string parameterName) => TechnicalId.IsValid(value.Value)
        ? value
        : throw new ArgumentException("The technical identifier is invalid.", parameterName);
}

using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;

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

/// <summary>A bounded external-effect intent durably enqueued after a successful service node.</summary>
public sealed record ExternalEffectIntent
{
    /// <summary>Maximum UTF-8 size of one external-effect payload.</summary>
    public const int MaximumPayloadBytes = 256 * 1_024;

    /// <summary>Creates an immutable intent. The operation identifier must be unique within one activation.</summary>
    public ExternalEffectIntent(
        TechnicalId operationId,
        TechnicalId destination,
        string contentType,
        CanonicalJson payload)
    {
        if (!TechnicalId.IsValid(operationId.Value))
        {
            throw new ArgumentException("The operation identifier is invalid.", nameof(operationId));
        }

        if (!TechnicalId.IsValid(destination.Value))
        {
            throw new ArgumentException("The destination identifier is invalid.", nameof(destination));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (contentType.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(contentType), "The content type cannot exceed 128 characters.");
        }

        OperationId = operationId;
        Destination = destination;
        ContentType = contentType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        if (Payload.CanonicalByteCount > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload),
                $"The external-effect payload cannot exceed {MaximumPayloadBytes} UTF-8 bytes.");
        }
    }

    /// <summary>Gets the stable operation identifier within the node activation.</summary>
    public TechnicalId OperationId { get; }

    /// <summary>Gets the logical destination resolved by an outbox transport.</summary>
    public TechnicalId Destination { get; }

    /// <summary>Gets the payload media type.</summary>
    public string ContentType { get; }

    /// <summary>Gets the canonical JSON payload.</summary>
    public CanonicalJson Payload { get; }
}

/// <summary>Result returned by a service-node handler.</summary>
public sealed class ServiceNodeResult
{
    private ServiceNodeResult(
        NodeExecutionDisposition disposition,
        NodeStateUpdate stateUpdate,
        TechnicalId? errorCode,
        ImmutableArray<ExternalEffectIntent> effects)
    {
        Disposition = disposition;
        StateUpdate = stateUpdate;
        ErrorCode = errorCode;
        Effects = effects;
    }

    /// <summary>Gets the bounded outcome category.</summary>
    public NodeExecutionDisposition Disposition { get; }

    /// <summary>Gets the successful state update, or <see cref="NodeStateUpdate.Unchanged"/> on failure.</summary>
    public NodeStateUpdate StateUpdate { get; }

    /// <summary>Gets the qualified machine-readable failure code.</summary>
    public TechnicalId? ErrorCode { get; }

    /// <summary>Gets external effects to enqueue atomically after a successful attempt.</summary>
    public ImmutableArray<ExternalEffectIntent> Effects { get; }

    /// <summary>Creates a successful result. The runtime chooses the next node.</summary>
    public static ServiceNodeResult Success(
        NodeStateUpdate? stateUpdate = null,
        IEnumerable<ExternalEffectIntent>? effects = null)
    {
        var immutableEffects = effects?.ToImmutableArray() ?? [];
        if (immutableEffects.Any(item => item is null) ||
            immutableEffects.Select(item => item.OperationId.Value).Distinct(StringComparer.Ordinal).Count() != immutableEffects.Length)
        {
            throw new ArgumentException("Effect operation identifiers must be unique within one result.", nameof(effects));
        }

        return new(NodeExecutionDisposition.Succeeded, stateUpdate ?? NodeStateUpdate.Unchanged, null, immutableEffects);
    }

    /// <summary>Creates a retryable failure. The worker owns delay and retry limits.</summary>
    public static ServiceNodeResult Retryable(TechnicalId errorCode) =>
        new(NodeExecutionDisposition.RetryableFailure, NodeStateUpdate.Unchanged, Validate(errorCode), []);

    /// <summary>Creates a permanent failure.</summary>
    public static ServiceNodeResult PermanentFailure(TechnicalId errorCode) =>
        new(NodeExecutionDisposition.PermanentFailure, NodeStateUpdate.Unchanged, Validate(errorCode), []);

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

/// <summary>Deterministic bounded result of validating a human-task submission.</summary>
public sealed class HumanTaskCompletionResult
{
    private HumanTaskCompletionResult(
        bool succeeded,
        TechnicalId? outcome,
        NodeStateUpdate stateUpdate,
        TechnicalId? errorCode,
        ImmutableArray<ExternalEffectIntent> effects,
        ImmutableArray<DesignatedTaskAssignment> designatedAssignments)
    {
        Succeeded = succeeded;
        Outcome = outcome;
        StateUpdate = stateUpdate;
        ErrorCode = errorCode;
        Effects = effects;
        DesignatedAssignments = designatedAssignments;
    }

    public bool Succeeded { get; }
    public TechnicalId? Outcome { get; }
    public NodeStateUpdate StateUpdate { get; }
    public TechnicalId? ErrorCode { get; }
    public ImmutableArray<ExternalEffectIntent> Effects { get; }
    public ImmutableArray<DesignatedTaskAssignment> DesignatedAssignments { get; }

    public static HumanTaskCompletionResult Success(
        TechnicalId outcome,
        NodeStateUpdate? stateUpdate = null,
        IEnumerable<ExternalEffectIntent>? effects = null,
        IEnumerable<DesignatedTaskAssignment>? designatedAssignments = null)
    {
        var effectSnapshot = effects?.ToImmutableArray() ?? [];
        if (effectSnapshot.Select(item => item.OperationId.Value).Distinct(StringComparer.Ordinal).Count() != effectSnapshot.Length)
            throw new ArgumentException("Effect operation identifiers must be unique.", nameof(effects));
        return new(true, outcome, stateUpdate ?? NodeStateUpdate.Unchanged, null, effectSnapshot,
            designatedAssignments?.ToImmutableArray() ?? []);
    }

    public static HumanTaskCompletionResult Rejected(TechnicalId errorCode) =>
        new(false, null, NodeStateUpdate.Unchanged, errorCode, [], []);
}

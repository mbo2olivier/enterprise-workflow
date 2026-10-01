namespace EnterpriseWorkflow.Persistence;

/// <summary>
/// Provider-independent outcome of one atomic store operation.
/// </summary>
public enum StoreOutcome
{
    /// <summary>The mutation was committed.</summary>
    Succeeded,

    /// <summary>The same command had already been committed.</summary>
    Idempotent,

    /// <summary>A persisted precondition did not match.</summary>
    Conflict,

    /// <summary>The requested durable resource does not exist.</summary>
    NotFound,

    /// <summary>The provider could not complete the operation.</summary>
    Unavailable,
}

/// <summary>
/// Structured result returned after the transaction boundary.
/// </summary>
public sealed record StoreResult<T>(StoreOutcome Outcome, T? Value, string? ErrorCode = null);

/// <summary>
/// Creates typed store results without static members on generic API types.
/// </summary>
public static class StoreResults
{
    /// <summary>Creates a committed result.</summary>
    public static StoreResult<T> Succeeded<T>(T value) => new(StoreOutcome.Succeeded, value);

    /// <summary>Creates a previously committed result.</summary>
    public static StoreResult<T> Idempotent<T>(T value) => new(StoreOutcome.Idempotent, value);

    /// <summary>Creates a conflict result.</summary>
    public static StoreResult<T> Conflict<T>(string errorCode) => new(StoreOutcome.Conflict, default, errorCode);

    /// <summary>Creates a missing-resource result.</summary>
    public static StoreResult<T> NotFound<T>(string errorCode) => new(StoreOutcome.NotFound, default, errorCode);

    /// <summary>Creates a provider-unavailable result.</summary>
    public static StoreResult<T> Unavailable<T>(string errorCode) => new(StoreOutcome.Unavailable, default, errorCode);
}

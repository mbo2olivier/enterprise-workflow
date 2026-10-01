namespace EnterpriseWorkflow.Persistence;

/// <summary>
/// Pure reference rules shared by provider conformance scenarios.
/// </summary>
public static class StoreContractRules
{
    /// <summary>Classifies a repeated immutable publication.</summary>
    public static StoreOutcome ClassifyPublication(string? persistedSha256, string requestedSha256) =>
        persistedSha256 is null
            ? StoreOutcome.Succeeded
            : string.Equals(persistedSha256, requestedSha256, StringComparison.Ordinal)
                ? StoreOutcome.Idempotent
                : StoreOutcome.Conflict;

    /// <summary>Classifies a repeated command receipt in the same scope and key.</summary>
    public static StoreOutcome ClassifyReceipt(string? persistedRequestSha256, string requestedRequestSha256) =>
        persistedRequestSha256 is null
            ? StoreOutcome.Succeeded
            : string.Equals(persistedRequestSha256, requestedRequestSha256, StringComparison.Ordinal)
                ? StoreOutcome.Idempotent
                : StoreOutcome.Conflict;

    /// <summary>Checks whether a lease token can still fence a commit at the provider's current time.</summary>
    public static bool IsCurrentLease(
        WorkItemStatus status,
        LeaseToken persistedToken,
        long persistedGeneration,
        DateTimeOffset expiresAtUtc,
        LeaseToken presentedToken,
        long presentedGeneration,
        DateTimeOffset storeUtcNow) =>
        status is WorkItemStatus.Leased &&
        persistedToken == presentedToken &&
        persistedGeneration == presentedGeneration &&
        expiresAtUtc > storeUtcNow &&
        HasUtcMillisecondPrecision(expiresAtUtc) &&
        HasUtcMillisecondPrecision(storeUtcNow);

    /// <summary>Checks the accepted UTC and millisecond precision contract.</summary>
    public static bool HasUtcMillisecondPrecision(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero && value.Ticks % TimeSpan.TicksPerMillisecond == 0;

    /// <summary>Checks the accepted instance transition table.</summary>
    public static bool CanTransition(WorkflowInstanceStatus from, WorkflowInstanceStatus to) =>
        (from, to) switch
        {
            (WorkflowInstanceStatus.Created, WorkflowInstanceStatus.Running) => true,
            (WorkflowInstanceStatus.Created, WorkflowInstanceStatus.Cancelled) => true,
            (WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Running) => true,
            (WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Waiting) => true,
            (WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Completed) => true,
            (WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Failed) => true,
            (WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Cancelled) => true,
            (WorkflowInstanceStatus.Waiting, WorkflowInstanceStatus.Running) => true,
            (WorkflowInstanceStatus.Waiting, WorkflowInstanceStatus.Cancelled) => true,
            _ => false,
        };
}


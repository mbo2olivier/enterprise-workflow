namespace EnterpriseWorkflow.Persistence;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EnterpriseWorkflow.Abstractions;

/// <summary>
/// Pure reference rules shared by provider conformance scenarios.
/// </summary>
public static class StoreContractRules
{
    /// <summary>Normalizes optional text because Oracle persists an empty string as null.</summary>
    public static string? NormalizeOptionalText(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// Computes the portable physical key for a start receipt from length-prefixed UTF-8 components.
    /// Persisted components must still be compared to defend against corruption or a theoretical collision.
    /// </summary>
    public static string ComputeStartReceiptKey(StartCommandScope scope, TechnicalId idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(scope);
        using var stream = new MemoryStream();
        WriteLengthPrefixed(stream, scope.InstallationId.Value);
        WriteLengthPrefixed(stream, scope.CommandTypeId.Value);
        WriteLengthPrefixed(stream, scope.Actor.ProviderId.Value);
        WriteLengthPrefixed(stream, scope.Actor.SubjectId);
        WriteLengthPrefixed(stream, idempotencyKey.Value);
        return Convert.ToHexStringLower(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }

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

    private static void WriteLengthPrefixed(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }
}

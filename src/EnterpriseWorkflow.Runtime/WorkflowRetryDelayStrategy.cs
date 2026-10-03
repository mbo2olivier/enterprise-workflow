namespace EnterpriseWorkflow.Runtime;

/// <summary>Computes a retry delay; implementations can make jitter deterministic in tests.</summary>
public interface IWorkflowRetryDelayStrategy
{
    TimeSpan GetDelay(int completedAttempts, TimeSpan baseDelay, TimeSpan maximumDelay);
}

/// <summary>Exponential backoff with full jitter.</summary>
public sealed class FullJitterWorkflowRetryDelayStrategy : IWorkflowRetryDelayStrategy
{
    public TimeSpan GetDelay(int completedAttempts, TimeSpan baseDelay, TimeSpan maximumDelay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(completedAttempts);
        var exponent = Math.Min(completedAttempts - 1, 30);
        var ceiling = Math.Min(maximumDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * Math.Pow(2, exponent));
        return TimeSpan.FromMilliseconds(Random.Shared.NextInt64(0, checked((long)Math.Ceiling(ceiling)) + 1));
    }
}

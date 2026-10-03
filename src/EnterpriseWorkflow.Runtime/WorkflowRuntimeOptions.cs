namespace EnterpriseWorkflow.Runtime;

/// <summary>Enterprise-configurable worker, lease, polling, timeout and retry policy.</summary>
public sealed class WorkflowRuntimeOptions
{
    public int WorkerConcurrency { get; set; } = 1;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan IdlePollingInterval { get; set; } = TimeSpan.FromMilliseconds(250);
    public int MaximumAttempts { get; set; } = 5;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan RetryMaximumDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int OutboxConcurrency { get; set; } = 1;
    public TimeSpan OutboxLeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan OutboxIdlePollingInterval { get; set; } = TimeSpan.FromMilliseconds(250);
    public int OutboxMaximumAttempts { get; set; } = 10;
    public TimeSpan OutboxRetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan OutboxRetryMaximumDelay { get; set; } = TimeSpan.FromMinutes(1);

    internal void Validate()
    {
        Positive(WorkerConcurrency, nameof(WorkerConcurrency));
        Positive(OutboxConcurrency, nameof(OutboxConcurrency));
        Positive(MaximumAttempts, nameof(MaximumAttempts));
        Positive(OutboxMaximumAttempts, nameof(OutboxMaximumAttempts));
        PositiveMilliseconds(LeaseDuration, nameof(LeaseDuration));
        PositiveMilliseconds(LeaseRenewalInterval, nameof(LeaseRenewalInterval));
        PositiveMilliseconds(IdlePollingInterval, nameof(IdlePollingInterval));
        PositiveMilliseconds(RetryBaseDelay, nameof(RetryBaseDelay));
        PositiveMilliseconds(RetryMaximumDelay, nameof(RetryMaximumDelay));
        PositiveMilliseconds(AttemptTimeout, nameof(AttemptTimeout));
        PositiveMilliseconds(OutboxLeaseDuration, nameof(OutboxLeaseDuration));
        PositiveMilliseconds(OutboxIdlePollingInterval, nameof(OutboxIdlePollingInterval));
        PositiveMilliseconds(OutboxRetryBaseDelay, nameof(OutboxRetryBaseDelay));
        PositiveMilliseconds(OutboxRetryMaximumDelay, nameof(OutboxRetryMaximumDelay));
        if (LeaseRenewalInterval >= LeaseDuration)
            throw new InvalidOperationException("LeaseRenewalInterval must be shorter than LeaseDuration.");
        if (RetryBaseDelay > RetryMaximumDelay || OutboxRetryBaseDelay > OutboxRetryMaximumDelay)
            throw new InvalidOperationException("A retry base delay cannot exceed its maximum delay.");
    }

    private static void Positive(int value, string name)
    {
        if (value <= 0) throw new InvalidOperationException($"{name} must be strictly positive.");
    }

    private static void PositiveMilliseconds(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value.Ticks % TimeSpan.TicksPerMillisecond != 0)
            throw new InvalidOperationException($"{name} must be a strictly positive whole number of milliseconds.");
    }
}

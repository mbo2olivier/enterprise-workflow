namespace EnterpriseWorkflow.Persistence;

public sealed record WorkflowTatQuery(string? ProcessId = null, int? ProcessVersion = null, string? NodeId = null,
    ActorIdentity? Analyst = null, bool ByAnalyst = false, DateTimeOffset? CompletedFromUtc = null,
    DateTimeOffset? CompletedUntilUtc = null);
public sealed record WorkflowTatRow(string ProcessId, int ProcessVersion, string NodeId, ActorIdentity? Analyst,
    int CompletedCount, double? MeanMilliseconds, long? MinimumMilliseconds, long? MaximumMilliseconds,
    int OpenCount, long? OldestOpenMilliseconds);

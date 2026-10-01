using EnterpriseWorkflow.Persistence;
using Xunit;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class StoreContractTests
{
    [Fact]
    public void PublicationIsIdempotentOnlyForIdenticalContent()
    {
        Assert.Equal(StoreOutcome.Succeeded, StoreContractRules.ClassifyPublication(null, "hash-a"));
        Assert.Equal(StoreOutcome.Idempotent, StoreContractRules.ClassifyPublication("hash-a", "hash-a"));
        Assert.Equal(StoreOutcome.Conflict, StoreContractRules.ClassifyPublication("hash-a", "hash-b"));
    }

    [Fact]
    public void ReceiptIsIdempotentOnlyForIdenticalRequest()
    {
        Assert.Equal(StoreOutcome.Succeeded, StoreContractRules.ClassifyReceipt(null, "request-a"));
        Assert.Equal(StoreOutcome.Idempotent, StoreContractRules.ClassifyReceipt("request-a", "request-a"));
        Assert.Equal(StoreOutcome.Conflict, StoreContractRules.ClassifyReceipt("request-a", "request-b"));
    }

    [Fact]
    public void LeaseRequiresTokenGenerationAndUnexpiredStoreTime()
    {
        var token = new LeaseToken(Guid.NewGuid());
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.True(StoreContractRules.IsCurrentLease(
            WorkItemStatus.Leased,
            token,
            4,
            now.AddSeconds(30),
            token,
            4,
            now));
        Assert.False(StoreContractRules.IsCurrentLease(
            WorkItemStatus.Leased,
            token,
            4,
            now,
            token,
            4,
            now));
        Assert.False(StoreContractRules.IsCurrentLease(
            WorkItemStatus.Leased,
            token,
            4,
            now.AddSeconds(30),
            new LeaseToken(Guid.NewGuid()),
            4,
            now));
        Assert.False(StoreContractRules.IsCurrentLease(
            WorkItemStatus.Leased,
            token,
            4,
            now.AddSeconds(30),
            token,
            3,
            now));
    }

    [Fact]
    public void TimeContractRequiresUtcAtMillisecondPrecision()
    {
        var valid = new DateTimeOffset(2026, 10, 1, 12, 0, 0, 123, TimeSpan.Zero);
        var nonUtc = valid.ToOffset(TimeSpan.FromHours(1));
        var subMillisecond = valid.AddTicks(1);

        Assert.True(StoreContractRules.HasUtcMillisecondPrecision(valid));
        Assert.False(StoreContractRules.HasUtcMillisecondPrecision(nonUtc));
        Assert.False(StoreContractRules.HasUtcMillisecondPrecision(subMillisecond));
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Created, WorkflowInstanceStatus.Running, true)]
    [InlineData(WorkflowInstanceStatus.Created, WorkflowInstanceStatus.Completed, false)]
    [InlineData(WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Waiting, true)]
    [InlineData(WorkflowInstanceStatus.Waiting, WorkflowInstanceStatus.Running, true)]
    [InlineData(WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Running, false)]
    [InlineData(WorkflowInstanceStatus.Failed, WorkflowInstanceStatus.Running, false)]
    [InlineData(WorkflowInstanceStatus.Cancelled, WorkflowInstanceStatus.Running, false)]
    public void InstanceTransitionTableIsExplicit(
        WorkflowInstanceStatus from,
        WorkflowInstanceStatus to,
        bool expected)
    {
        Assert.Equal(expected, StoreContractRules.CanTransition(from, to));
    }
}


using EnterpriseWorkflow.Core.Model;
using Xunit;
namespace EnterpriseWorkflow.Core.Tests;
public sealed class PublicReferenceTests
{
    [Fact]
    public void ReferencesHaveTenBase36CharactersAndRejectAnExhaustedClockRange()
    {
        var time = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var references = Enumerable.Range(0, 1000).AsParallel().Select(_ => WorkflowPublicReference.Create(time)).ToArray();
        Assert.All(references, reference => Assert.True(WorkflowPublicReference.IsValid(reference)));
        Assert.Single(references.Select(reference => reference[..5]).Distinct(StringComparer.Ordinal));
        Assert.Equal(1000, references.Distinct(StringComparer.Ordinal).Count());
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowPublicReference.Create(time.AddYears(-2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowPublicReference.Create(time.AddYears(200)));
    }
}

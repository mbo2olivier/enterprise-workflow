using System.Text.Json.Serialization;
using EnterpriseWorkflow.Core.Serialization;
using Xunit;
namespace EnterpriseWorkflow.Core.Tests;
public sealed class WorkflowSerializerTests
{
    [Fact]
    public void CanonicalSerializationUsesStableNamesAndEnforcesConfiguredBudgets()
    {
        var options = new WorkflowSerializationOptions { MaximumStateBytes = 80, MaximumDepth = 4 };
        var serializer = new WorkflowSerializer(options);
        options.MaximumStateBytes = 1;
        var value = serializer.Serialize(new State { Value = "été" });
        Assert.Equal("été", serializer.Deserialize<State>(value).Value);
        Assert.True(serializer.IsValidState(value));
        Assert.Throws<FormatException>(() => serializer.Serialize(new State { Value = new string('x', 100) }));
        Assert.Throws<FormatException>(() => serializer.Serialize(new[] { Guid.NewGuid().ToString("N") }));
        Assert.Throws<System.Text.Json.JsonException>(() => serializer.Deserialize<State>(EnterpriseWorkflow.Core.Model.CanonicalJson.CreateObject("{\"unexpected\":1}", 100)));
        Assert.Throws<InvalidOperationException>(() => new WorkflowSerializer(new() { MaximumStateBytes = int.MaxValue }));
    }
    public sealed class State { [JsonPropertyName("businessValue")] public string Value { get; init; } = string.Empty; }
}

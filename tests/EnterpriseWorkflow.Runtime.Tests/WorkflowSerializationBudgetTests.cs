using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Sdk;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EnterpriseWorkflow.Runtime.Tests;
public sealed class WorkflowSerializationBudgetTests
{
    [Fact]
    public async Task StaticReplacementCannotBypassTheConfiguredCommitBudget()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-serialization-budget-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var token = TestContext.Current.CancellationToken;
        try
        {
            var database = new SqliteWorkflowDatabase("Data Source=" + Path.Combine(root, "workflow.db")); await database.MigrateAsync(token); var store = new SqliteWorkflowStore(database);
            var definition = WorkflowBuilder.Create("budget", 1).Start("start").Service("large", "large", 1).End("end").Then("start", "large").Then("large", "end").Validate().Definition!;
            var published = await store.PublishDefinitionAsync(new(definition), token);
            var started = await store.StartInstanceAsync(new(new(new("installation"), new("start"), new(new("local"), "alice")), new("start"), "hash", published.Value!, WorkflowState.Create("{\"Value\":\"small\"}", 1), null, null, DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())), token);
            var services = new ServiceCollection(); services.AddSingleton<IWorkflowStore>(store);
            services.AddEnterpriseWorkflowHandlers(handlers => handlers.AddService<LargeHandler>("large", 1));
            services.AddEnterpriseWorkflowRuntime<Transport>(options => options.Serialization.MaximumStateBytes = 64);
            await using var provider = services.BuildServiceProvider(); var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
            Assert.True(await pump.ExecuteNextAsync(new("worker"), token)); Assert.True(await pump.ExecuteNextAsync(new("worker"), token));
            var state = (await store.ReadInstanceAsync(started.Value!.InstanceId, token)).Value!;
            Assert.Equal(WorkflowInstanceStatus.Failed, state.Summary.Status); Assert.Equal("small", state.State.Value.ToJsonElement().GetProperty("Value").GetString());
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
    private sealed class LargeHandler : IServiceNodeHandler
    {
        public ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken) => ValueTask.FromResult(ServiceNodeResult.Success(NodeStateUpdate.Replace(new { Value = new string('x', 1000) })));
    }
    private sealed class Transport : IExternalEffectTransport
    {
        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

var compilation = WorkflowBuilder.Create("sample.executable", 1)
    .Start("start")
    .Service("prepare", "sample.prepare")
    .End("end")
    .Then("start", "prepare")
    .Then("prepare", "end")
    .Validate();
var definition = compilation.Definition ?? throw new InvalidOperationException(
    string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));

var path = Path.Combine(Path.GetTempPath(), $"enterprise-workflow-sample-{Guid.NewGuid():N}.db");
try
{
    var connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    var database = new SqliteWorkflowDatabase(connectionString);
    await database.MigrateAsync(CancellationToken.None);
    var store = new SqliteWorkflowStore(database);

    var services = new ServiceCollection();
    services.AddSingleton<IWorkflowStore>(store);
    services.AddEnterpriseWorkflowHandlers(registry => registry.AddService<PrepareHandler>("sample.prepare", 1));
    services.AddEnterpriseWorkflowRuntime<ConsoleTransport>();
    await using var provider = services.BuildServiceProvider();

    var registry = provider.GetRequiredService<IWorkflowHandlerRegistry>();
    WorkflowDefinitionBindingValidator.Validate(definition, registry).EnsureValid();
    await store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), CancellationToken.None);
    await store.StartInstanceAsync(new StartInstanceCommand(
        new StartCommandScope(new TechnicalId("sample.installation"), new TechnicalId("workflow.start"),
            new ActorIdentity(new TechnicalId("sample"), "operator")),
        new TechnicalId("sample-command"), "sample-request",
        new DefinitionReference(definition.Id, definition.Version, definition.Sha256),
        WorkflowState.Create("{\"prepared\":false}", 1), null, null,
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())), CancellationToken.None);

    var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
    while (await pump.ExecuteNextAsync(new TechnicalId("sample.worker"), CancellationToken.None)) { }
    var outbox = provider.GetRequiredService<IWorkflowOutboxPump>();
    while (await outbox.DeliverNextAsync(new TechnicalId("sample.dispatcher"), CancellationToken.None)) { }

    Console.WriteLine($"Workflow {definition.Id} v{definition.Version} terminé ; état et effet externe ont été persistés durablement.");
    return 0;
}
finally
{
    if (File.Exists(path)) File.Delete(path);
}

internal sealed class PrepareHandler : IServiceNodeHandler
{
    public ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        using var replacement = JsonDocument.Parse("{\"prepared\":true}");
        return ValueTask.FromResult(ServiceNodeResult.Success(
            NodeStateUpdate.Replace(replacement.RootElement),
            [new ExternalEffectIntent(new TechnicalId("prepared"), new TechnicalId("console"), "application/json",
                CanonicalJson.CreateObject("{\"message\":\"ready\"}", 1024))]));
    }
}

internal sealed class ConsoleTransport : IExternalEffectTransport
{
    public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Effet {message.OperationId} livré à {message.Destination}; clé={message.IdempotencyKey}");
        return ValueTask.FromResult(ExternalEffectDeliveryResult.Success());
    }
}

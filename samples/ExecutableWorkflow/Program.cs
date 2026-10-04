using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Sdk;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

var compilation = WorkflowBuilder.Create("sample.executable", 1)
    .Start("start")
    .Service("prepare", "sample.prepare")
    .HumanTask("approve", HumanTaskKind.Approval, HumanTaskAssignmentMode.DesignatedIdentity,
        "sample.approval-form", 1, "sample.complete-approval",
        [new HumanTaskActionDraft("task.approve", "approved")])
    .End("end")
    .Then("start", "prepare")
    .Then("prepare", "approve")
    .On("approve", "approved", "end")
    .Validate();
var definition = compilation.Definition ?? throw new InvalidOperationException(
    string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));

var path = Path.Combine(Path.GetTempPath(), $"enterprise-workflow-sample-{Guid.NewGuid():N}.db");
try
{
    var connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = false,
    }.ToString();
    var database = new SqliteWorkflowDatabase(connectionString);
    await database.MigrateAsync(CancellationToken.None);
    var store = new SqliteWorkflowStore(database);

    var services = new ServiceCollection();
    services.AddSingleton<IWorkflowStore>(store);
    services.AddSingleton<IWorkflowAuthorizationService, AllowSampleAuthorization>();
    services.AddEnterpriseWorkflowHandlers(registry => registry
        .AddService<PrepareHandler>("sample.prepare", 1)
        .AddHumanTask<ApprovalHandler>("sample.complete-approval", 1));
    services.AddEnterpriseWorkflowRuntime<ConsoleTransport>();
    await using var provider = services.BuildServiceProvider();

    var registry = provider.GetRequiredService<IWorkflowHandlerRegistry>();
    WorkflowDefinitionBindingValidator.Validate(definition, registry).EnsureValid();
    await store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), CancellationToken.None);
    var started = await provider.GetRequiredService<IWorkflowStartService>().StartAsync(definition, new StartInstanceCommand(
        new StartCommandScope(new TechnicalId("sample.installation"), new TechnicalId("workflow.start"),
            new ActorIdentity(new TechnicalId("sample"), "requester")),
        new TechnicalId("sample-command"), "sample-request",
        new DefinitionReference(definition.Id, definition.Version, definition.Sha256),
        WorkflowState.Create("{\"prepared\":false}", 1), null, null,
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
        [new DesignatedTaskAssignment(new TechnicalId("approve"),
            new ActorIdentity(new TechnicalId("sample"), "approver"))]), CancellationToken.None);
    if (!started.Succeeded) throw new InvalidOperationException($"Démarrage refusé : {started.ErrorCode}");

    var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
    while (await pump.ExecuteNextAsync(new TechnicalId("sample.worker"), CancellationToken.None)) { }
    var openTasks = await store.ReadOpenHumanTasksAsync(new ReadOpenHumanTasksCommand(null, 10), CancellationToken.None);
    var task = openTasks.Value?.Tasks.Single() ?? throw new InvalidOperationException("La tâche d'approbation n'a pas été créée.");
    var humanTasks = provider.GetRequiredService<IHumanTaskService>();
    var approval = await humanTasks.CompleteAsync(
        task.TaskId,
        task.Revision,
        new IdentityReference("sample", "approver"),
        WorkflowActions.ApproveTask,
        new TechnicalId("sample-approval"),
        CanonicalJson.CreateObject("{\"comment\":\"approved\"}", 1024),
        CancellationToken.None);
    if (!approval.Succeeded) throw new InvalidOperationException($"Approbation refusée : {approval.ErrorCode}");
    while (await pump.ExecuteNextAsync(new TechnicalId("sample.worker"), CancellationToken.None)) { }
    var outbox = provider.GetRequiredService<IWorkflowOutboxPump>();
    while (await outbox.DeliverNextAsync(new TechnicalId("sample.dispatcher"), CancellationToken.None)) { }

    Console.WriteLine($"Workflow {definition.Id} v{definition.Version} terminé ; service, tâche humaine et effet externe ont été persistés durablement.");
    return 0;
}
finally
{
    SqliteConnection.ClearAllPools();
    DeleteIfExists(path);
    DeleteIfExists(path + "-shm");
    DeleteIfExists(path + "-wal");
}

static void DeleteIfExists(string filePath)
{
    if (File.Exists(filePath)) File.Delete(filePath);
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

internal sealed class ApprovalHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(
        HumanTaskCompletionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(HumanTaskCompletionResult.Success(new TechnicalId("approved")));
}

internal sealed class AllowSampleAuthorization : IWorkflowAuthorizationService
{
    public ValueTask<AuthorizationDecision> AuthorizeAsync(
        WorkflowAuthorizationRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AuthorizationDecision(true, "sample.allow"));
}

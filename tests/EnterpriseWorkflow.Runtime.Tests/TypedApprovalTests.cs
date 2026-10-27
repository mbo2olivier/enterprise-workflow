using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using EnterpriseWorkflow.Security;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class TypedApprovalTests
{
    [Fact]
    public async Task ApprovalCommentsAreAtomicServerAttributedAndNotDuplicatedOnReplay()
    {
        var root = Path.Combine(Path.GetTempPath(), "typed-approval-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var token = TestContext.Current.CancellationToken;
        try
        {
            var database = new SqliteWorkflowDatabase("Data Source=" + Path.Combine(root, "workflow.db")); await database.MigrateAsync(token);
            var store = new SqliteWorkflowStore(database); var module = Module(); var presentation = new WorkflowPresentationRegistryBuilder();
            module.ConfigurePresentation(presentation, new WorkflowPermissionRegistryBuilder(module.Info).Add("act", "Act").Permissions);
            var declaration = Assert.Single(presentation.Processes); var definition = declaration.DefinitionFactory(new(new("approval"), "1.0.0", new string('a', 64)));
            var published = await store.PublishDefinitionAsync(new(definition), token);
            var started = await store.StartInstanceAsync(new(new(new("installation"), new("workflow.start"), new(new("local"), "requester")),
                new("start"), "hash", published.Value!, WorkflowState.Create(JsonSerializer.Serialize(new ApprovalState { Reason = "Initial" }), 1), null, null,
                DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())), token);
            var services = new ServiceCollection(); services.AddSingleton<IWorkflowStore>(store); services.AddSingleton<IWorkflowAuthorizationService, Authorization>();
            services.AddEnterpriseWorkflowHandlers(module.ConfigureHandlers); services.AddEnterpriseWorkflowRuntime<Transport>();
            await using var provider = services.BuildServiceProvider(); var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
            Assert.True(await pump.ExecuteNextAsync(new("worker"), token)); Assert.True(await pump.ExecuteNextAsync(new("worker"), token));
            var open = Assert.Single((await store.ReadOpenHumanTasksAsync(new(null, 10), token)).Value!.Tasks);
            var task = (await store.GetHumanTaskAsync(open.TaskId, token)).Value!; var human = provider.GetRequiredService<IHumanTaskService>();
            Assert.True((await human.ClaimAsync(task.TaskId, task.Revision, new("local", "analyst"), token)).Succeeded);
            task = (await store.GetHumanTaskAsync(task.TaskId, token)).Value!;
            Assert.Equal("runtime.comment-required", (await human.CompleteAsync(task.TaskId, task.Revision, new("local", "analyst"), new("decline"), new("missing"), Json("{}"), token)).ErrorCode);
            var blocked = await human.CompleteAsync(task.TaskId, task.Revision, new("local", "analyst"), new("blocked"), new("blocked"), Json("{\"comment\":\"Should not persist\"}"), token);
            Assert.Equal("runtime.state-invalid", blocked.ErrorCode);
            Assert.Empty((await store.ReadInstanceAsync(started.Value!.InstanceId, token)).Value!.State.Value.Deserialize<ApprovalState>().Reviews);
            var submission = Json("""{"comment":" Refus motivé ","actorSubjectId":"forged","createdAtUtc":"2000-01-01"}""");
            var complete = await human.CompleteAsync(task.TaskId, task.Revision, new("local", "analyst"), new("decline"), new("decline"), submission, token);
            Assert.True(complete.Succeeded, complete.ErrorCode);
            Assert.True((await human.CompleteAsync(task.TaskId, task.Revision, new("local", "analyst"), new("decline"), new("decline"), submission, token)).WasReplay);
            var state = (await store.ReadInstanceAsync(started.Value!.InstanceId, token)).Value!.State.Value.Deserialize<ApprovalState>();
            var entry = Assert.Single(state.Reviews);
            Assert.Equal("Refus motivé", entry.Text); Assert.Equal("analyst", entry.ActorSubjectId); Assert.Equal("local", entry.ActorProviderId);
            Assert.Equal("review", entry.StageId); Assert.Equal("decline", entry.ActionId); Assert.Equal(TimeSpan.Zero, entry.CreatedAtUtc.Offset); Assert.True(entry.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OptionalCommentAndInvalidStateDoNotCreatePartialUpdates()
    {
        var module = Module(); var registry = new WorkflowPresentationRegistryBuilder();
        module.ConfigurePresentation(registry, new WorkflowPermissionRegistryBuilder(module.Info).Add("act", "Act").Permissions);
        var definition = Assert.Single(registry.Processes).DefinitionFactory(new(new("approval"), "1.0.0", new string('a', 64)));
        var node = definition.Nodes.Single(item => item.Id.Value == "review");
        var handler = new StandardHumanTaskHandler();
        HumanTaskCompletionContext Context(string action, string submission, string state) => new(new(Guid.NewGuid()), new(Guid.NewGuid()), new(Guid.NewGuid()), new("review"),
            new(new("local"), "analyst"), new(action), Json(submission), WorkflowState.Create(state, 1), node.Configuration) { CompletionAtUtc = DateTimeOffset.UtcNow };
        var result = await handler.CompleteAsync(Context("approve", "{\"comment\":\"  \"}", "{\"Reason\":\"ok\",\"review_log\":[]}"), TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded); Assert.False(result.StateUpdate.ReplacesState);
        var invalid = await handler.CompleteAsync(Context("decline", "{\"comment\":\"no\"}", "{\"Reason\":\"ok\",\"review_log\":17}"), TestContext.Current.CancellationToken);
        Assert.False(invalid.Succeeded); Assert.False(invalid.StateUpdate.ReplacesState);
        var valid = await handler.CompleteAsync(Context("decline", "{\"comment\":\"no\"}", "{\"Reason\":\"\",\"review_log\":[]}"), TestContext.Current.CancellationToken);
        Assert.NotEmpty(WorkflowStateContract.From(definition)!.Validate(Json(valid.StateUpdate.GetReplacement()!.Value.GetRawText())));
        var policy = WorkflowStagePolicy.Read(node.Configuration)!;
        var historical = policy with { Actions = policy.Actions.ToDictionary(pair => pair.Key, pair => pair.Value with { CaptureCommentActorName = false }, StringComparer.Ordinal) };
        var historicalContext = Context("decline", "{\"comment\":\"historic\"}", "{\"Reason\":\"ok\",\"review_log\":[]}") with { Configuration = Json(historical.ToConfigurationJson()) };
        var historicalResult = await handler.CompleteAsync(historicalContext, TestContext.Current.CancellationToken);
        Assert.True(historicalResult.Succeeded);
        Assert.False(historicalResult.StateUpdate.GetReplacement()!.Value.GetProperty("review_log")[0].TryGetProperty("actorDisplayName", out _));
    }

    [Fact]
    public void TypedReadsAreDetachedStrictAndReplacementIsExplicit()
    {
        var state = WorkflowState.Create("{\"Reason\":\"old\",\"review_log\":[]}", 1);
        var context = new NodeExecutionContext(new(Guid.NewGuid()), new(Guid.NewGuid()), new("service"), 1, state, Json("{}"));
        var copy = context.ReadState<ApprovalState>(); copy.Reason = "new";
        Assert.Equal("old", context.ReadState<ApprovalState>().Reason);
        var replacement = NodeStateUpdate.Replace(copy);
        Assert.Equal("new", replacement.GetReplacement()!.Value.GetProperty("Reason").GetString());
        Assert.Throws<JsonException>(() => Json("{\"extra\":true}").Deserialize<ApprovalState>());
        Assert.Throws<ArgumentException>(() => NodeStateUpdate.Replace(new[] { Guid.NewGuid().ToString("N") }));
    }
    private static WorkflowModuleBuilder Module()
    {
        var module = new WorkflowModuleBuilder(new("approval", "1.0.0", 1));
        module.AddHumanTask<FormHandler>("form").AddForm("start", "Start", form => form.HandledBy<FormHandler>().Fields(fields => fields.Text("Reason", "Reason")));
        module.AddProcess(process =>
        {
            var flow = process.WithState<ApprovalState>().InitializedBy<Initializer>();
            flow.WithId("request").WithStateSchemaVersion(1).RequiresPermission("act").StartWithForm("start").Then("review");
            flow.Approval("review", "Review", stage => stage.RequiresPermission("act").ClaimedByEligibleParticipant().Actions(actions =>
            {
                actions.Add("decline", "Decline", action => action.RequireCommentAt(state => state.Reviews).Then("end"));
                actions.Add("blocked", "Blocked", action => action.RequireCommentAt(state => state.Reviews).ValidateState("blocked").Then("end"));
                actions.Add("approve", "Approve", action => action.CommentAt(state => state.Reviews).Then("end"));
            }));
            flow.End("end");
        }); return module;
    }
    public sealed class ApprovalState
    {
        [RequiredFor("blocked")] public string? Guard { get; set; }
        [Required] public string Reason { get; set; } = "";
        [JsonPropertyName("review_log")] public List<WorkflowComment> Reviews { get; init; } = [];
    }
    private sealed class Initializer : IWorkflowStateInitializer
    {
        public ValueTask<WorkflowStateInitializationResult> InitializeAsync(WorkflowStateInitializationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(new WorkflowStateInitializationResult(context.Submission));
    }
    private sealed class FormHandler : IHumanTaskCompletionHandler
    {
        public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken) => ValueTask.FromResult(HumanTaskCompletionResult.Success());
    }
    private sealed class Authorization : IWorkflowAuthorizationService
    {
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(true, "test"));
    }
    private sealed class Transport : IExternalEffectTransport
    {
        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken) => ValueTask.FromResult(ExternalEffectDeliveryResult.Success());
    }
    private static CanonicalJson Json(string text) => CanonicalJson.CreateObject(text, 256 * 1024);
}

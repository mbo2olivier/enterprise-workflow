using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Sdk;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class FluentHumanTaskTests
{
    [Fact]
    public async Task ConfirmationPoolConditionalAssignmentReplayAndRevocationSurviveRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"l8b-{Guid.NewGuid():N}.db");
        var token = TestContext.Current.CancellationToken;
        try
        {
            var database = new SqliteWorkflowDatabase(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            await database.MigrateAsync(token);
            var store = new SqliteWorkflowStore(database);
            var definition = Definition();
            var published = await store.PublishDefinitionAsync(new(definition), token);
            var started = await store.StartInstanceAsync(new(new(new("test"), new("workflow.start"), new(new("local"), "requester")),
                new("start-key"), "start-hash", published.Value!, WorkflowState.Create("{\"reason\":\"Family\"}", 1), null, null,
                DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())), token);
            Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
            var authorization = new Authorization();
            await using var provider = Services(store, authorization);
            var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
            var human = provider.GetRequiredService<IHumanTaskService>();
            await pump.ExecuteNextAsync(new("worker"), token);
            await pump.ExecuteNextAsync(new("worker"), token);
            var confirmation = await TaskAt(store, "confirmation", token);
            Assert.Equal("requester", confirmation.Assignee!.SubjectId);
            Assert.True((await human.CompleteAsync(confirmation.TaskId, confirmation.Revision, new("local", "requester"),
                new("submit"), new("confirm"), Json("{}"), token)).Succeeded);
            await pump.ExecuteNextAsync(new("worker"), token);
            var hr = await TaskAt(store, "hr", token);
            Assert.Equal(HumanTaskStatus.Available, hr.Status);
            Assert.True((await human.ClaimAsync(hr.TaskId, hr.Revision, new("local", "hr-person"), token)).Succeeded);
            hr = (await store.GetHumanTaskAsync(hr.TaskId, token)).Value!;
            Assert.False((await human.CompleteAsync(hr.TaskId, hr.Revision, new("local", "hr-person"),
                new("approve"), new("missing"), Json("{}"), token)).Succeeded);
            Assert.Equal("runtime.comment-required", (await human.CompleteAsync(hr.TaskId, hr.Revision, new("local", "hr-person"),
                new("decline"), new("empty-comment"), Json("{\"comment\":\" \"}"), token)).ErrorCode);
            var malformedSelection = HumanTaskSubmission.WithAssignments(Json("{}"), [new(new("manager"), new(new("local"), new string('x', 257)))]);
            Assert.Equal("runtime.invalid-assignment-submission", (await human.CompleteAsync(hr.TaskId, hr.Revision,
                new("local", "hr-person"), new("approve"), new("invalid-subject"), malformedSelection, token)).ErrorCode);
            var submission = HumanTaskSubmission.WithAssignments(Json("{}"), [new(new("manager"), new(new("local"), "manager-person"))]);
            var completed = await human.CompleteAsync(hr.TaskId, hr.Revision, new("local", "hr-person"), new("approve"), new("approve-key"), submission, token);
            Assert.True(completed.Succeeded, completed.ErrorCode);
            Assert.True((await human.CompleteAsync(hr.TaskId, hr.Revision, new("local", "hr-person"), new("approve"), new("approve-key"), submission, token)).WasReplay);
            var changed = HumanTaskSubmission.WithAssignments(Json("{}"), [new(new("manager"), new(new("local"), "someone-else"))]);
            Assert.Equal("runtime.human-task-idempotency-conflict", (await human.CompleteAsync(hr.TaskId, hr.Revision,
                new("local", "hr-person"), new("approve"), new("approve-key"), changed, token)).ErrorCode);

            authorization.Denied.Add("manager-person");
            await using var restarted = Services(store, authorization);
            await restarted.GetRequiredService<IWorkflowExecutionPump>().ExecuteNextAsync(new("restarted"), token);
            var manager = await TaskAt(store, "manager", token);
            Assert.Equal(HumanTaskStatus.AwaitingAssignment, manager.Status);
            Assert.Null(manager.Assignee);
            authorization.Denied.Clear();
            var recovered = await restarted.GetRequiredService<IHumanTaskService>().AssignAsync(manager.TaskId, manager.Revision,
                new("local", "hr-person"), new("local", "manager-person"), token);
            Assert.True(recovered.Succeeded, recovered.ErrorCode);
            manager = (await store.GetHumanTaskAsync(manager.TaskId, token)).Value!;
            Assert.True((await human.CompleteAsync(manager.TaskId, manager.Revision, new("local", "manager-person"),
                new("approve"), new("final-key"), Json("{}"), token)).Succeeded);
            await pump.ExecuteNextAsync(new("worker"), token);
            Assert.False(await pump.ExecuteNextAsync(new("worker"), token));

            // Notification pages and individual emails are committed atomically with parent delivery.
            var notification = await store.ClaimDueOutboxAsync(new(new("mailer"), TimeSpan.FromSeconds(30)), token);
            Assert.Equal("workflow.pool-notification", notification.Value!.Destination.Value);
            var lease = notification.Value;
            var followUps = new[] { new OutboxWrite(new("recipient-1"), new("workflow.pool-mail"), "application/json", "{}") };
            Assert.Equal(StoreOutcome.Succeeded, (await store.CommitOutboxAsync(new(lease.MessageId, lease.Token,
                lease.Generation, OutboxCommitKind.Delivered, null, null, followUps), token)).Outcome);
            var child = await store.ClaimDueOutboxAsync(new(new("mailer"), TimeSpan.FromSeconds(30)), token);
            Assert.Equal("recipient-1", child.Value!.OperationId.Value);
            Assert.Equal(lease.ActivationId, child.Value.ActivationId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }

    private static ServiceProvider Services(IWorkflowStore store, Authorization authorization)
    {
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton<IWorkflowAuthorizationService>(authorization);
        services.AddEnterpriseWorkflowHandlers(registry => registry.AddHumanTask<StandardHumanTaskHandler>("standard", 1));
        services.AddEnterpriseWorkflowRuntime<Transport>(options => options.NotifyHumanTaskPools = true);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
    private static WorkflowDefinition Definition()
    {
        string Policy(bool initiator, bool previous, params (string Id, string? Target, bool Comment)[] actions) => new WorkflowStagePolicy
        {
            AssignedToInitiator = initiator, AssignedFromPreviousTask = previous,
            Permissions = [["test.act"]], Actions = actions.ToDictionary(action => action.Id, action => new WorkflowActionPolicy
            { Label = action.Id, AssigneeTarget = action.Target, RequireComment = action.Comment }),
        }.ToConfigurationJson();
        return WorkflowBuilder.Create("l8b", 1).Start("start", new WorkflowStagePolicy { Permissions = [["test.act"]] }.ToConfigurationJson())
            .HumanTask("confirmation", HumanTaskKind.Input, HumanTaskAssignmentMode.DesignatedIdentity, "form", 1, "standard", [new("submit", "submit")], configurationJson: Policy(true, false, ("submit", null, false)))
            .HumanTask("hr", HumanTaskKind.Approval, HumanTaskAssignmentMode.EligiblePool, "form", 1, "standard", [new("approve", "approve"), new("decline", "decline")], configurationJson: Policy(false, false, ("approve", "manager", false), ("decline", null, true)))
            .HumanTask("manager", HumanTaskKind.Approval, HumanTaskAssignmentMode.DesignatedIdentity, "form", 1, "standard", [new("approve", "approve")], configurationJson: Policy(false, true, ("approve", null, false)))
            .End("end").Then("start", "confirmation").On("confirmation", "submit", "hr").On("hr", "approve", "manager").On("hr", "decline", "end").On("manager", "approve", "end").Validate().Definition!;
    }
    private static async Task<HumanTaskSnapshot> TaskAt(SqliteWorkflowStore store, string stage, CancellationToken token)
    {
        var tasks = await store.ReadOpenHumanTasksAsync(new(null, 50), token);
        var task = Assert.Single(tasks.Value!.Tasks, task => task.NodeId.Value == stage);
        return (await store.GetHumanTaskAsync(task.TaskId, token)).Value!;
    }
    private static CanonicalJson Json(string value) => CanonicalJson.CreateObject(value, 10000);
    private sealed class Authorization : IWorkflowAuthorizationService
    {
        public HashSet<string> Denied { get; } = new(StringComparer.Ordinal);
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuthorizationDecision(!Denied.Contains(request.Identity.SubjectId), "test"));
    }
    private sealed class Transport : IExternalEffectTransport
    {
        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ExternalEffectDeliveryResult.Success());
    }
}

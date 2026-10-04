using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Oracle;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Persistence.Oracle.Tests;

[CollectionDefinition("Oracle integration", DisableParallelization = true)]
public sealed class OracleIntegrationSet;

[Collection("Oracle integration")]
public sealed class OracleWorkflowStoreTests
{
    [Fact]
    public async Task MigrationAndStartAreDurableAndIdempotent()
    {
        var fixture = await ResetDatabaseAsync();
        await fixture.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var definition = BuildDefinition();
        var publication = await fixture.Store.PublishDefinitionAsync(
            new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var repeatedPublication = await fixture.Store.PublishDefinitionAsync(
            new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var command = CreateStartCommand(definition, "request-a");
        var started = await fixture.Store.StartInstanceAsync(command, TestContext.Current.CancellationToken);
        var reopened = CreateStore(fixture.ConnectionString);
        var repeatedStart = await reopened.StartInstanceAsync(command, TestContext.Current.CancellationToken);
        var conflict = await reopened.StartInstanceAsync(
            command with { RequestSha256 = "request-b" }, TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, publication.Outcome);
        Assert.Equal(StoreOutcome.Idempotent, repeatedPublication.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
        Assert.Equal(StoreOutcome.Idempotent, repeatedStart.Outcome);
        Assert.Equal(started.Value!.InstanceId, repeatedStart.Value!.InstanceId);
        Assert.Equal(StoreOutcome.Conflict, conflict.Outcome);
    }

    [Fact]
    public async Task ConcurrentClaimsHaveOneWinner()
    {
        var fixture = await CreateStartedStoreAsync();
        var first = CreateStore(fixture.ConnectionString);
        var second = CreateStore(fixture.ConnectionString);
        var claims = await Task.WhenAll(
            first.ClaimDueWorkAsync(new ClaimDueWorkCommand(new TechnicalId("worker.one"), TimeSpan.FromSeconds(30)),
                TestContext.Current.CancellationToken).AsTask(),
            second.ClaimDueWorkAsync(new ClaimDueWorkCommand(new TechnicalId("worker.two"), TimeSpan.FromSeconds(30)),
                TestContext.Current.CancellationToken).AsTask());

        Assert.Single(claims, result => result.Outcome is StoreOutcome.Succeeded);
        Assert.Single(claims, result => result.Outcome is StoreOutcome.NotFound);
        var winner = Assert.IsType<ClaimedWork>(claims.Single(result => result.Outcome is StoreOutcome.Succeeded).Value).Lease;
        Assert.Equal(StoreOutcome.Succeeded,
            (await fixture.Store.CommitNodeResultAsync(Complete(winner), TestContext.Current.CancellationToken)).Outcome);
    }

    [Fact]
    public async Task ExpiredOwnerIsFencedAndDatabaseCanResumeToCompletion()
    {
        var fixture = await CreateStartedStoreAsync();
        var oldLease = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.old"), TimeSpan.FromMilliseconds(1)),
            TestContext.Current.CancellationToken)).Value).Lease;
        await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
        var reopened = CreateStore(fixture.ConnectionString);
        var newLease = Assert.IsType<ClaimedWork>((await reopened.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.new"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value).Lease;
        var staleCommit = await fixture.Store.CommitNodeResultAsync(Complete(oldLease), TestContext.Current.CancellationToken);
        var continued = await reopened.CommitNodeResultAsync(new CommitNodeResultCommand(
            newLease.WorkItemId, newLease.Token, newLease.Generation, newLease.InstanceRevision,
            NodeCommitKind.Continue, WorkflowState.Create("{\"step\":1}", 1),
            new NextWork(new TechnicalId("end"), newLease.StoreUtcNow), null), TestContext.Current.CancellationToken);
        var finalLease = Assert.IsType<ClaimedWork>((await reopened.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.final"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value).Lease;
        var completed = await reopened.CommitNodeResultAsync(Complete(finalLease), TestContext.Current.CancellationToken);

        Assert.Equal(oldLease.WorkItemId, newLease.WorkItemId);
        Assert.Equal(oldLease.Generation + 1, newLease.Generation);
        Assert.Equal(StoreOutcome.Conflict, staleCommit.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, continued.Outcome);
        Assert.Equal(WorkflowInstanceStatus.Completed, completed.Value!.InstanceStatus);
    }

    [Fact]
    public async Task CancellationInvalidatesOutstandingLeaseAtomically()
    {
        var fixture = await CreateStartedStoreAsync();
        var lease = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value).Lease;
        var cancelled = await fixture.Store.CancelInstanceAsync(new CancelInstanceCommand(
            lease.InstanceId, lease.InstanceRevision, new ActorIdentity(new TechnicalId("local"), "operator"),
            lease.StoreUtcNow), TestContext.Current.CancellationToken);
        var commit = await fixture.Store.CommitNodeResultAsync(Complete(lease), TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, cancelled.Outcome);
        Assert.Equal(StoreOutcome.Conflict, commit.Outcome);
    }

    [Fact]
    public async Task RevisionConflictDoesNotPartiallyCommitAndCurrentOwnerCanRetry()
    {
        var fixture = await CreateStartedStoreAsync();
        var lease = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value).Lease;
        var conflict = await fixture.Store.CommitNodeResultAsync(
            Complete(lease) with { ExpectedInstanceRevision = lease.InstanceRevision + 1 },
            TestContext.Current.CancellationToken);
        var retried = await fixture.Store.CommitNodeResultAsync(Complete(lease), TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Conflict, conflict.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, retried.Outcome);
    }

    [Fact]
    public async Task OutboxIsAtomicAndExpiredDeliveryOwnerIsFenced()
    {
        var fixture = await CreateStartedStoreAsync();
        var work = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        var committed = await fixture.Store.CommitNodeResultAsync(new CommitNodeResultCommand(
            work.Lease.WorkItemId, work.Lease.Token, work.Lease.Generation, work.Lease.InstanceRevision,
            NodeCommitKind.Complete, null, null, null,
            [new OutboxWrite(new TechnicalId("notify"), new TechnicalId("receiver"), "application/json", "{\"ok\":true}")]),
            TestContext.Current.CancellationToken);
        Assert.Equal(StoreOutcome.Succeeded, committed.Outcome);

        var oldLease = Assert.IsType<OutboxLease>((await fixture.Store.ClaimDueOutboxAsync(
            new ClaimDueOutboxCommand(new TechnicalId("dispatcher.old"), TimeSpan.FromMilliseconds(10)),
            TestContext.Current.CancellationToken)).Value);
        await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
        var newLease = Assert.IsType<OutboxLease>((await fixture.Store.ClaimDueOutboxAsync(
            new ClaimDueOutboxCommand(new TechnicalId("dispatcher.new"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);

        var stale = await fixture.Store.CommitOutboxAsync(new CommitOutboxCommand(
            oldLease.MessageId, oldLease.Token, oldLease.Generation, OutboxCommitKind.Delivered, null, null),
            TestContext.Current.CancellationToken);
        var delivered = await fixture.Store.CommitOutboxAsync(new CommitOutboxCommand(
            newLease.MessageId, newLease.Token, newLease.Generation, OutboxCommitKind.Delivered, null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(oldLease.IdempotencyKey, newLease.IdempotencyKey);
        Assert.Equal(oldLease.Generation + 1, newLease.Generation);
        Assert.Equal(StoreOutcome.Conflict, stale.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, delivered.Outcome);
    }

    [Fact]
    public async Task RuntimeExecutesStartServiceEndAndDeliversOutboxOnOracle()
    {
        var fixture = await ResetDatabaseAsync();
        var definition = Assert.IsType<WorkflowDefinition>(WorkflowBuilder.Create("oracle.runtime", 1)
            .Start("start").Service("service", "oracle.execute").End("end")
            .Then("start", "service").Then("service", "end").Validate().Definition);
        Assert.Equal(StoreOutcome.Succeeded, (await fixture.Store.PublishDefinitionAsync(
            new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(StoreOutcome.Succeeded, (await fixture.Store.StartInstanceAsync(
            CreateStartCommand(definition, "runtime-request"), TestContext.Current.CancellationToken)).Outcome);

        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowStore>(fixture.Store);
        services.AddEnterpriseWorkflowHandlers(registry => registry.AddService<OracleRuntimeHandler>("oracle.execute", 1));
        services.AddEnterpriseWorkflowRuntime<OracleTestTransport>(options =>
        {
            options.LeaseDuration = TimeSpan.FromSeconds(10);
            options.LeaseRenewalInterval = TimeSpan.FromSeconds(2);
        });
        await using var provider = services.BuildServiceProvider();
        var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
        var owner = new TechnicalId("oracle.runtime.worker");
        Assert.True(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken));
        Assert.True(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken));
        Assert.True(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken));
        Assert.False(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken));

        var outbox = provider.GetRequiredService<IWorkflowOutboxPump>();
        Assert.True(await outbox.DeliverNextAsync(new TechnicalId("oracle.runtime.outbox"), TestContext.Current.CancellationToken));
        Assert.False(await outbox.DeliverNextAsync(new TechnicalId("oracle.runtime.outbox"), TestContext.Current.CancellationToken));
        Assert.Equal(1, Assert.IsType<OracleTestTransport>(provider.GetRequiredService<IExternalEffectTransport>()).Deliveries);
    }

    [Fact]
    public async Task OptionalEmptyTextIsPersistedAsNull()
    {
        var fixture = await ResetDatabaseAsync();
        var definition = BuildDefinition();
        await fixture.Store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var command = CreateStartCommand(definition, "request-empty") with { BusinessKey = string.Empty, CorrelationId = string.Empty };
        Assert.Equal(StoreOutcome.Succeeded,
            (await fixture.Store.StartInstanceAsync(command, TestContext.Current.CancellationToken)).Outcome);

        await using var context = fixture.Database.CreateDbContext();
        var values = await context.Database.SqlQueryRaw<OptionalTextProbe>(
            "SELECT \"BUSINESS_KEY\" AS \"BusinessKey\", \"CORRELATION_ID\" AS \"CorrelationId\" FROM \"EW_INSTANCES\"")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(values.BusinessKey);
        Assert.Null(values.CorrelationId);
    }

    [Fact]
    public async Task HumanTaskReceiptAndTimerWakeupAreQualifiedOnOracle()
    {
        var fixture = await ResetDatabaseAsync();
        var definition = Assert.IsType<WorkflowDefinition>(WorkflowBuilder.Create("oracle.human", 1)
            .Start("start")
            .HumanTask("approve", HumanTaskKind.Approval, HumanTaskAssignmentMode.DesignatedIdentity,
                "approval.form", 1, "approval.complete",
                [new HumanTaskActionDraft("task.approve", "approved")])
            .End("end").Then("start", "approve").On("approve", "approved", "end").Validate().Definition);
        await fixture.Store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var actor = new ActorIdentity(new TechnicalId("local"), "approver");
        await fixture.Store.StartInstanceAsync(CreateStartCommand(definition, "human-request") with
        {
            DesignatedAssignments = [new DesignatedTaskAssignment(new TechnicalId("approve"), actor)],
        }, TestContext.Current.CancellationToken);
        var start = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("oracle.human.start"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        await fixture.Store.CommitNodeResultAsync(new CommitNodeResultCommand(
            start.Lease.WorkItemId, start.Lease.Token, start.Lease.Generation, start.Lease.InstanceRevision,
            NodeCommitKind.Continue, null, new NextWork(new TechnicalId("approve"), start.Lease.StoreUtcNow), null),
            TestContext.Current.CancellationToken);
        var wait = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("oracle.human.wait"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        var taskCommit = await fixture.Store.CommitNodeResultAsync(new CommitNodeResultCommand(
            wait.Lease.WorkItemId, wait.Lease.Token, wait.Lease.Generation, wait.Lease.InstanceRevision,
            NodeCommitKind.WaitHumanTask, null, null, null,
            HumanTask: new HumanTaskWait(HumanTaskAssignmentMode.DesignatedIdentity)),
            TestContext.Current.CancellationToken);
        var taskId = taskCommit.Value!.HumanTaskId!.Value;
        var openedTask = await fixture.Store.GetHumanTaskAsync(taskId, TestContext.Current.CancellationToken);
        var complete = new CompleteHumanTaskCommand(
            taskId, 0, taskCommit.Value.Revision, actor, new TechnicalId("task.approve"),
            new TechnicalId("oracle-completion"), new string('a', 64), null,
            new NextWork(new TechnicalId("end"), MillisecondUtcNow()));
        var first = await fixture.Store.CompleteHumanTaskAsync(complete, TestContext.Current.CancellationToken);
        var replay = await fixture.Store.CompleteHumanTaskAsync(complete, TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, taskCommit.Outcome);
        Assert.Equal(HumanTaskStatus.Assigned, openedTask.Value!.Status);
        Assert.Equal(StoreOutcome.Succeeded, first.Outcome);
        Assert.Equal(StoreOutcome.Idempotent, replay.Outcome);
    }

    [Fact]
    public async Task DueTimerCanBeRecoveredAndFiredOnceOnOracle()
    {
        var fixture = await ResetDatabaseAsync();
        var definition = Assert.IsType<WorkflowDefinition>(WorkflowBuilder.Create("oracle.timer", 1)
            .Start("start").Timer("timer", TimeSpan.FromMilliseconds(1)).End("end")
            .Then("start", "timer").Then("timer", "end").Validate().Definition);
        await fixture.Store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        await fixture.Store.StartInstanceAsync(CreateStartCommand(definition, "timer-request"), TestContext.Current.CancellationToken);
        var start = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("oracle.timer.start"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        await fixture.Store.CommitNodeResultAsync(new CommitNodeResultCommand(
            start.Lease.WorkItemId, start.Lease.Token, start.Lease.Generation, start.Lease.InstanceRevision,
            NodeCommitKind.Continue, null, new NextWork(new TechnicalId("timer"), start.Lease.StoreUtcNow), null),
            TestContext.Current.CancellationToken);
        var wait = Assert.IsType<ClaimedWork>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("oracle.timer.wait"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        await fixture.Store.CommitNodeResultAsync(new CommitNodeResultCommand(
            wait.Lease.WorkItemId, wait.Lease.Token, wait.Lease.Generation, wait.Lease.InstanceRevision,
            NodeCommitKind.WaitTimer, null, null, null,
            Timer: new TimerWait(TimeSpan.FromMilliseconds(1), new TechnicalId("end"))),
            TestContext.Current.CancellationToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        var fired = await fixture.Store.FireNextDueTimerAsync(TestContext.Current.CancellationToken);
        var none = await fixture.Store.FireNextDueTimerAsync(TestContext.Current.CancellationToken);
        Assert.Equal(StoreOutcome.Succeeded, fired.Outcome);
        Assert.Equal(StoreOutcome.NotFound, none.Outcome);
    }

    private static async Task<Fixture> CreateStartedStoreAsync()
    {
        var fixture = await ResetDatabaseAsync();
        var definition = BuildDefinition();
        Assert.Equal(StoreOutcome.Succeeded,
            (await fixture.Store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(StoreOutcome.Succeeded,
            (await fixture.Store.StartInstanceAsync(CreateStartCommand(definition, "request-a"),
                TestContext.Current.CancellationToken)).Outcome);
        return fixture;
    }

    private static async Task<Fixture> ResetDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING must target a dedicated disposable test schema.");
        var database = new OracleWorkflowDatabase(connectionString);
        await using (var context = database.CreateDbContext())
        {
            await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }

        await database.MigrateAsync(TestContext.Current.CancellationToken);
        return new Fixture(connectionString, database, new OracleWorkflowStore(database));
    }

    private static OracleWorkflowStore CreateStore(string connectionString) =>
        new(new OracleWorkflowDatabase(connectionString));

    private static CommitNodeResultCommand Complete(WorkLease lease) => new(
        lease.WorkItemId, lease.Token, lease.Generation, lease.InstanceRevision,
        NodeCommitKind.Complete, null, null, null);

    private static WorkflowDefinition BuildDefinition()
    {
        var compilation = WorkflowBuilder.Create("oracle.contract", 1)
            .Start("start").End("end").Then("start", "end").Validate();
        return Assert.IsType<WorkflowDefinition>(compilation.Definition);
    }

    private static StartInstanceCommand CreateStartCommand(WorkflowDefinition definition, string requestHash) => new(
        new StartCommandScope(new TechnicalId("installation.test"), new TechnicalId("workflow.start"),
            new ActorIdentity(new TechnicalId("local"), "subject-1")),
        new TechnicalId("command-1"), requestHash,
        new DefinitionReference(definition.Id, definition.Version, definition.Sha256),
        WorkflowState.Create("{\"step\":0}", 1), "business-1", "correlation-1",
        new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    private static DateTimeOffset MillisecondUtcNow() =>
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private sealed record Fixture(string ConnectionString, OracleWorkflowDatabase Database, OracleWorkflowStore Store);

    private sealed class OptionalTextProbe
    {
        public string? BusinessKey { get; set; }
        public string? CorrelationId { get; set; }
    }

    private sealed class OracleRuntimeHandler : IServiceNodeHandler
    {
        public ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ServiceNodeResult.Success(effects:
            [
                new ExternalEffectIntent(new TechnicalId("notify"), new TechnicalId("oracle.receiver"),
                    "application/json", CanonicalJson.CreateObject("{\"qualified\":true}", 1024)),
            ]));
    }

    private sealed class OracleTestTransport : IExternalEffectTransport
    {
        public int Deliveries { get; private set; }
        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            Deliveries++;
            return ValueTask.FromResult(ExternalEffectDeliveryResult.Success());
        }
    }
}

using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Sdk;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EnterpriseWorkflow.Persistence.Sqlite.Tests;

public sealed class SqliteWorkflowStoreTests
{
    [Fact]
    public async Task MigrationAndStartAreDurableAndIdempotent()
    {
        await using var databaseFile = new TemporaryDatabase();
        var database = new SqliteWorkflowDatabase(databaseFile.ConnectionString);
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = new SqliteWorkflowStore(database);
        var definition = BuildDefinition();

        var publication = await store.PublishDefinitionAsync(
            new PublishDefinitionCommand(definition),
            TestContext.Current.CancellationToken);
        var repeatedPublication = await store.PublishDefinitionAsync(
            new PublishDefinitionCommand(definition),
            TestContext.Current.CancellationToken);
        var command = CreateStartCommand(definition, "request-a");
        var started = await store.StartInstanceAsync(command, TestContext.Current.CancellationToken);

        var reopenedStore = new SqliteWorkflowStore(new SqliteWorkflowDatabase(databaseFile.ConnectionString));
        var repeatedStart = await reopenedStore.StartInstanceAsync(command, TestContext.Current.CancellationToken);
        var conflictingStart = await reopenedStore.StartInstanceAsync(
            command with { RequestSha256 = "request-b" },
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, publication.Outcome);
        Assert.Equal(StoreOutcome.Idempotent, repeatedPublication.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
        Assert.True(started.Value!.WasCreated);
        Assert.Equal(StoreOutcome.Idempotent, repeatedStart.Outcome);
        Assert.False(repeatedStart.Value!.WasCreated);
        Assert.Equal(started.Value.InstanceId, repeatedStart.Value.InstanceId);
        Assert.Equal(StoreOutcome.Conflict, conflictingStart.Outcome);
    }

    [Fact]
    public async Task ModuleInventoryBlocksReferencedRemovalAndContentReplacement()
    {
        await using var databaseFile = new TemporaryDatabase();
        var database = new SqliteWorkflowDatabase(databaseFile.ConnectionString);
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = new SqliteWorkflowStore(database);
        var artifact = new ModuleArtifactReference(new TechnicalId("fixture.approval"), "1.0.0", new string('a', 64));

        var installed = await store.ReconcileModuleArtifactsAsync(
            new ReconcileModuleArtifactsCommand([artifact]), TestContext.Current.CancellationToken);
        var definition = BuildDefinition(artifact);
        await store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var removed = await store.ReconcileModuleArtifactsAsync(
            new ReconcileModuleArtifactsCommand([]), TestContext.Current.CancellationToken);
        var replaced = await store.ReconcileModuleArtifactsAsync(
            new ReconcileModuleArtifactsCommand([artifact with { Sha256 = new string('b', 64) }]),
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, installed.Outcome);
        Assert.Equal(artifact, Assert.Single(installed.Value!.InstalledArtifacts));
        Assert.Equal((StoreOutcome.Conflict, "EW3051_REQUIRED_MODULE_ARTIFACT_MISSING"),
            (removed.Outcome, removed.ErrorCode));
        Assert.Equal((StoreOutcome.Conflict, "EW3051_REQUIRED_MODULE_ARTIFACT_MISSING"),
            (replaced.Outcome, replaced.ErrorCode));
    }

    [Fact]
    public async Task ExplicitStateMigrationIsAtomicAuditedAndRefusesAnActiveInstance()
    {
        await using var databaseFile = new TemporaryDatabase();
        var database = new SqliteWorkflowDatabase(databaseFile.ConnectionString);
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = new SqliteWorkflowStore(database);
        var artifact = new ModuleArtifactReference(new TechnicalId("fixture.approval"), "1.0.0", new string('a', 64));
        await store.ReconcileModuleArtifactsAsync(new ReconcileModuleArtifactsCommand([artifact]),
            TestContext.Current.CancellationToken);
        var definition = BuildDefinition(artifact);
        await store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var started = await store.StartInstanceAsync(CreateStartCommand(definition, "migration-request"),
            TestContext.Current.CancellationToken);
        var snapshot = await store.ReadStateMigrationSnapshotAsync(started.Value!.InstanceId,
            TestContext.Current.CancellationToken);
        var actor = new ActorIdentity(new TechnicalId("local"), "migration-operator");

        var migrated = await store.CommitStateMigrationAsync(new CommitStateMigrationCommand(
            started.Value.InstanceId, snapshot.Value!.Revision, 1,
            WorkflowState.Create("{\"migrated\":true}", 2), artifact, actor),
            TestContext.Current.CancellationToken);
        var stale = await store.CommitStateMigrationAsync(new CommitStateMigrationCommand(
            started.Value.InstanceId, snapshot.Value.Revision, 1,
            WorkflowState.Create("{\"migratedAgain\":true}", 2), artifact, actor),
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.migration"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken);
        var activeSnapshot = await store.ReadStateMigrationSnapshotAsync(started.Value.InstanceId,
            TestContext.Current.CancellationToken);
        var active = await store.CommitStateMigrationAsync(new CommitStateMigrationCommand(
            started.Value.InstanceId, activeSnapshot.Value!.Revision, 2,
            WorkflowState.Create("{\"migrated\":3}", 3), artifact, actor),
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, migrated.Outcome);
        Assert.Equal((1L, 2), (migrated.Value!.Revision, migrated.Value.SchemaVersion));
        Assert.Equal((StoreOutcome.Conflict, "EW3054_STALE_STATE_MIGRATION"), (stale.Outcome, stale.ErrorCode));
        Assert.Equal(StoreOutcome.Succeeded, claimed.Outcome);
        Assert.Equal((StoreOutcome.Conflict, "EW3056_STATE_MIGRATION_INSTANCE_ACTIVE"),
            (active.Outcome, active.ErrorCode));
        await using var connection = new SqliteConnection(databaseFile.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"EwAudits\" WHERE \"EventType\" = 'StateMigrated'";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ConcurrentClaimsHaveOneWinner()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var firstStore = new SqliteWorkflowStore(new SqliteWorkflowDatabase(fixture.DatabaseFile.ConnectionString));
        var secondStore = new SqliteWorkflowStore(new SqliteWorkflowDatabase(fixture.DatabaseFile.ConnectionString));

        var claims = await Task.WhenAll(
            firstStore.ClaimDueWorkAsync(
                new ClaimDueWorkCommand(new TechnicalId("worker.one"), TimeSpan.FromSeconds(30)),
                TestContext.Current.CancellationToken).AsTask(),
            secondStore.ClaimDueWorkAsync(
                new ClaimDueWorkCommand(new TechnicalId("worker.two"), TimeSpan.FromSeconds(30)),
                TestContext.Current.CancellationToken).AsTask());

        Assert.Single(claims, result => result.Outcome is StoreOutcome.Succeeded);
        Assert.Single(claims, result => result.Outcome is StoreOutcome.NotFound);
    }

    [Fact]
    public async Task ExpiredOwnerIsFencedAndFileCanResumeToCompletion()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var firstLeaseResult = await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.old"), TimeSpan.FromMilliseconds(1)),
            TestContext.Current.CancellationToken);
        var firstLease = Assert.IsType<ClaimedWork>(firstLeaseResult.Value).Lease;
        await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);

        var reopenedStore = new SqliteWorkflowStore(new SqliteWorkflowDatabase(fixture.DatabaseFile.ConnectionString));
        var secondLeaseResult = await reopenedStore.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.new"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken);
        var secondLease = Assert.IsType<ClaimedWork>(secondLeaseResult.Value).Lease;
        var staleCommit = await fixture.Store.CommitNodeResultAsync(
            Complete(firstLease),
            TestContext.Current.CancellationToken);
        var firstCommit = await reopenedStore.CommitNodeResultAsync(
            new CommitNodeResultCommand(
                secondLease.WorkItemId,
                secondLease.Token,
                secondLease.Generation,
                secondLease.InstanceRevision,
                NodeCommitKind.Continue,
                WorkflowState.Create("{\"step\":1}", 1),
                new NextWork(new TechnicalId("end"), secondLease.StoreUtcNow),
                null),
            TestContext.Current.CancellationToken);

        var finalLeaseResult = await reopenedStore.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.final"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken);
        var finalLease = Assert.IsType<ClaimedWork>(finalLeaseResult.Value).Lease;
        var finalCommit = await reopenedStore.CommitNodeResultAsync(
            Complete(finalLease),
            TestContext.Current.CancellationToken);

        Assert.Equal(firstLease.WorkItemId, secondLease.WorkItemId);
        Assert.Equal(firstLease.Generation + 1, secondLease.Generation);
        Assert.Equal(StoreOutcome.Conflict, staleCommit.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, firstCommit.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, finalCommit.Outcome);
        Assert.Equal(WorkflowInstanceStatus.Completed, finalCommit.Value!.InstanceStatus);
    }

    [Fact]
    public async Task CancellationInvalidatesOutstandingLeaseAtomically()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var leaseResult = await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken);
        var lease = Assert.IsType<ClaimedWork>(leaseResult.Value).Lease;

        var cancellation = await fixture.Store.CancelInstanceAsync(
            new CancelInstanceCommand(
                lease.InstanceId,
                lease.InstanceRevision,
                new ActorIdentity(new TechnicalId("local"), "operator"),
                lease.StoreUtcNow),
            TestContext.Current.CancellationToken);
        var commit = await fixture.Store.CommitNodeResultAsync(
            Complete(lease),
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, cancellation.Outcome);
        Assert.Equal(StoreOutcome.Conflict, commit.Outcome);
    }

    [Fact]
    public async Task RevisionConflictDoesNotPartiallyCommitAndCurrentOwnerCanRetry()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var leaseResult = await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken);
        var lease = Assert.IsType<ClaimedWork>(leaseResult.Value).Lease;
        var invalidCommit = Complete(lease) with { ExpectedInstanceRevision = lease.InstanceRevision + 1 };

        var conflict = await fixture.Store.CommitNodeResultAsync(
            invalidCommit,
            TestContext.Current.CancellationToken);
        var successfulRetry = await fixture.Store.CommitNodeResultAsync(
            Complete(lease),
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Conflict, conflict.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, successfulRetry.Outcome);
        Assert.Equal(lease.InstanceRevision + 1, successfulRetry.Value!.Revision);
    }

    [Fact]
    public async Task HumanTaskAssignmentClaimCompletionAndReceiptAreDurable()
    {
        await using var databaseFile = new TemporaryDatabase();
        var database = new SqliteWorkflowDatabase(databaseFile.ConnectionString);
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = new SqliteWorkflowStore(database);
        var definition = BuildHumanDefinition(HumanTaskAssignmentMode.DesignatedIdentity);
        await store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var assignee = new ActorIdentity(new TechnicalId("local"), "approver-1");
        var started = await store.StartInstanceAsync(
            CreateStartCommand(definition, "human-request") with
            {
                DesignatedAssignments = [new DesignatedTaskAssignment(new TechnicalId("approve"), assignee)],
            }, TestContext.Current.CancellationToken);

        var taskCommit = await AdvanceToWaitAsync(store, NodeCommitKind.WaitHumanTask,
            humanTask: new HumanTaskWait(HumanTaskAssignmentMode.DesignatedIdentity));
        var taskId = Assert.IsType<HumanTaskId>(taskCommit.Value!.HumanTaskId);
        var snapshot = await store.GetHumanTaskAsync(taskId, TestContext.Current.CancellationToken);
        Assert.Equal(HumanTaskStatus.Assigned, snapshot.Value!.Status);
        Assert.Equal(assignee, snapshot.Value.Assignee);
        Assert.Equal("subject-1", snapshot.Value.Initiator.SubjectId);

        var command = new CompleteHumanTaskCommand(
            taskId, 0, taskCommit.Value.Revision, assignee, new TechnicalId("task.approve"),
            new TechnicalId("completion-1"), new string('a', 64), null,
            new NextWork(new TechnicalId("end"), UtcNowMilliseconds()));
        var completed = await store.CompleteHumanTaskAsync(command, TestContext.Current.CancellationToken);
        var replayed = await store.CompleteHumanTaskAsync(command, TestContext.Current.CancellationToken);
        var conflict = await store.CompleteHumanTaskAsync(command with { RequestSha256 = new string('b', 64) },
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, completed.Outcome);
        Assert.Equal(StoreOutcome.Idempotent, replayed.Outcome);
        Assert.Equal(StoreOutcome.Conflict, conflict.Outcome);
        Assert.Equal(completed.Value!.InstanceRevision, replayed.Value!.InstanceRevision);
        Assert.Equal(StoreOutcome.Succeeded,
            (await store.GetHumanTaskCompletionReceiptAsync(taskId, new TechnicalId("completion-1"),
                TestContext.Current.CancellationToken)).Outcome);
    }

    [Fact]
    public async Task PoolClaimAndTimerCancellationRacesHaveOneWinner()
    {
        await using var taskDb = new TemporaryDatabase();
        var taskDatabase = new SqliteWorkflowDatabase(taskDb.ConnectionString);
        await taskDatabase.MigrateAsync(TestContext.Current.CancellationToken);
        var taskStore = new SqliteWorkflowStore(taskDatabase);
        var poolDefinition = BuildHumanDefinition(HumanTaskAssignmentMode.EligiblePool);
        await taskStore.PublishDefinitionAsync(new PublishDefinitionCommand(poolDefinition), TestContext.Current.CancellationToken);
        await taskStore.StartInstanceAsync(CreateStartCommand(poolDefinition, "pool-request"), TestContext.Current.CancellationToken);
        var taskCommit = await AdvanceToWaitAsync(taskStore, NodeCommitKind.WaitHumanTask,
            humanTask: new HumanTaskWait(HumanTaskAssignmentMode.EligiblePool));
        var taskId = taskCommit.Value!.HumanTaskId!.Value;
        var claims = await Task.WhenAll(
            taskStore.ClaimHumanTaskAsync(new ClaimHumanTaskCommand(taskId, 0,
                new ActorIdentity(new TechnicalId("local"), "maker-1")), TestContext.Current.CancellationToken).AsTask(),
            new SqliteWorkflowStore(new SqliteWorkflowDatabase(taskDb.ConnectionString)).ClaimHumanTaskAsync(
                new ClaimHumanTaskCommand(taskId, 0, new ActorIdentity(new TechnicalId("local"), "maker-2")),
                TestContext.Current.CancellationToken).AsTask());
        Assert.Single(claims, item => item.Outcome is StoreOutcome.Succeeded);
        Assert.Single(claims, item => item.Outcome is StoreOutcome.Conflict);

        await using var timerDb = new TemporaryDatabase();
        var timerDatabase = new SqliteWorkflowDatabase(timerDb.ConnectionString);
        await timerDatabase.MigrateAsync(TestContext.Current.CancellationToken);
        var timerStore = new SqliteWorkflowStore(timerDatabase);
        var timerDefinition = BuildTimerDefinition();
        await timerStore.PublishDefinitionAsync(new PublishDefinitionCommand(timerDefinition), TestContext.Current.CancellationToken);
        var timerStarted = await timerStore.StartInstanceAsync(
            CreateStartCommand(timerDefinition, "timer-request"), TestContext.Current.CancellationToken);
        var timerCommit = await AdvanceToWaitAsync(timerStore, NodeCommitKind.WaitTimer,
            timer: new TimerWait(TimeSpan.FromMilliseconds(1), new TechnicalId("end")));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var timerId = timerCommit.Value!.TimerId!.Value;
        var instanceId = timerStarted.Value!.InstanceId;
        var races = await Task.WhenAll(
            RaceTimerAsync(timerStore, timerId),
            RaceCancelAsync(new SqliteWorkflowStore(new SqliteWorkflowDatabase(timerDb.ConnectionString)), instanceId, timerCommit.Value.Revision));
        Assert.Single(races, item => item is StoreOutcome.Succeeded);
        Assert.Single(races, item => item is StoreOutcome.Conflict);
    }

    private static CommitNodeResultCommand Complete(WorkLease lease) =>
        new(
            lease.WorkItemId,
            lease.Token,
            lease.Generation,
            lease.InstanceRevision,
            NodeCommitKind.Complete,
            null,
            null,
            null);

    private static WorkflowDefinition BuildDefinition(ModuleArtifactReference? artifact = null)
    {
        var builder = WorkflowBuilder.Create("sqlite.contract", 1);
        if (artifact is not null) builder.RequiresArtifact(artifact.Id.Value, artifact.Version, artifact.Sha256);
        var compilation = builder.Start("start")
            .End("end")
            .Then("start", "end")
            .Validate();
        return Assert.IsType<WorkflowDefinition>(compilation.Definition);
    }

    private static WorkflowDefinition BuildHumanDefinition(HumanTaskAssignmentMode mode) =>
        Assert.IsType<WorkflowDefinition>(WorkflowBuilder.Create("sqlite.human", 1)
            .Start("start")
            .HumanTask("approve", HumanTaskKind.Approval, mode, "approval.form", 1, "approval.complete",
                [new HumanTaskActionDraft("task.approve", "approved")])
            .End("end")
            .Then("start", "approve").On("approve", "approved", "end").Validate().Definition);

    private static WorkflowDefinition BuildTimerDefinition() =>
        Assert.IsType<WorkflowDefinition>(WorkflowBuilder.Create("sqlite.timer", 1)
            .Start("start").Timer("timer", TimeSpan.FromMilliseconds(1)).End("end")
            .Then("start", "timer").Then("timer", "end").Validate().Definition);

    private static async Task<StoreResult<CommitNodeResult>> AdvanceToWaitAsync(
        SqliteWorkflowStore store, NodeCommitKind waitKind, HumanTaskWait? humanTask = null, TimerWait? timer = null)
    {
        var start = Assert.IsType<ClaimedWork>((await store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.start"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        await store.CommitNodeResultAsync(new CommitNodeResultCommand(
            start.Lease.WorkItemId, start.Lease.Token, start.Lease.Generation, start.Lease.InstanceRevision,
            NodeCommitKind.Continue, null, new NextWork(waitKind is NodeCommitKind.WaitTimer ? new TechnicalId("timer") : new TechnicalId("approve"), start.Lease.StoreUtcNow), null),
            TestContext.Current.CancellationToken);
        var wait = Assert.IsType<ClaimedWork>((await store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.wait"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        return await store.CommitNodeResultAsync(new CommitNodeResultCommand(
            wait.Lease.WorkItemId, wait.Lease.Token, wait.Lease.Generation, wait.Lease.InstanceRevision,
            waitKind, null, null, null, HumanTask: humanTask, Timer: timer), TestContext.Current.CancellationToken);
    }

    private static async Task<StoreOutcome> RaceTimerAsync(SqliteWorkflowStore store, WorkflowTimerId timerId) =>
        (await store.FireDueTimerAsync(new FireDueTimerCommand(timerId, 0), TestContext.Current.CancellationToken)).Outcome;

    private static async Task<StoreOutcome> RaceCancelAsync(
        SqliteWorkflowStore store, WorkflowInstanceId instanceId, long revision) =>
        (await store.CancelInstanceAsync(new CancelInstanceCommand(instanceId, revision,
            new ActorIdentity(new TechnicalId("local"), "operator"), UtcNowMilliseconds()),
            TestContext.Current.CancellationToken)).Outcome;

    private static DateTimeOffset UtcNowMilliseconds() =>
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private static StartInstanceCommand CreateStartCommand(WorkflowDefinition definition, string requestHash) =>
        new(
            new StartCommandScope(
                new TechnicalId("installation.test"),
                new TechnicalId("workflow.start"),
                new ActorIdentity(new TechnicalId("local"), "subject-1")),
            new TechnicalId("command-1"),
            requestHash,
            new DefinitionReference(definition.Id, definition.Version, definition.Sha256),
            WorkflowState.Create("{\"step\":0}", 1),
            "business-1",
            "correlation-1",
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class StoreFixture : IAsyncDisposable
    {
        private StoreFixture(TemporaryDatabase databaseFile, SqliteWorkflowStore store)
        {
            DatabaseFile = databaseFile;
            Store = store;
        }

        public TemporaryDatabase DatabaseFile { get; }

        public SqliteWorkflowStore Store { get; }

        public static async Task<StoreFixture> CreateAsync()
        {
            var databaseFile = new TemporaryDatabase();
            var database = new SqliteWorkflowDatabase(databaseFile.ConnectionString);
            await database.MigrateAsync(TestContext.Current.CancellationToken);
            var store = new SqliteWorkflowStore(database);
            var definition = BuildDefinition();
            var published = await store.PublishDefinitionAsync(
                new PublishDefinitionCommand(definition),
                TestContext.Current.CancellationToken);
            var started = await store.StartInstanceAsync(
                CreateStartCommand(definition, "request-a"),
                TestContext.Current.CancellationToken);
            Assert.Equal(StoreOutcome.Succeeded, published.Outcome);
            Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
            return new StoreFixture(databaseFile, store);
        }

        public ValueTask DisposeAsync() => DatabaseFile.DisposeAsync();
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"enterprise-workflow-{Guid.NewGuid():N}.db");

        public string ConnectionString => new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_path);
            File.Delete(_path + "-shm");
            File.Delete(_path + "-wal");
            return ValueTask.CompletedTask;
        }
    }
}

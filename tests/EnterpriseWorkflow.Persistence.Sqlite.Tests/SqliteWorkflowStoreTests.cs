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

    private static WorkflowDefinition BuildDefinition()
    {
        var compilation = WorkflowBuilder.Create("sqlite.contract", 1)
            .Start("start")
            .End("end")
            .Then("start", "end")
            .Validate();
        return Assert.IsType<WorkflowDefinition>(compilation.Definition);
    }

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

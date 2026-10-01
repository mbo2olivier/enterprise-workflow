using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Oracle;
using EnterpriseWorkflow.Sdk;
using Microsoft.EntityFrameworkCore;
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
        var winner = Assert.IsType<WorkLease>(claims.Single(result => result.Outcome is StoreOutcome.Succeeded).Value);
        Assert.Equal(StoreOutcome.Succeeded,
            (await fixture.Store.CommitNodeResultAsync(Complete(winner), TestContext.Current.CancellationToken)).Outcome);
    }

    [Fact]
    public async Task ExpiredOwnerIsFencedAndDatabaseCanResumeToCompletion()
    {
        var fixture = await CreateStartedStoreAsync();
        var oldLease = Assert.IsType<WorkLease>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.old"), TimeSpan.FromMilliseconds(1)),
            TestContext.Current.CancellationToken)).Value);
        await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
        var reopened = CreateStore(fixture.ConnectionString);
        var newLease = Assert.IsType<WorkLease>((await reopened.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.new"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        var staleCommit = await fixture.Store.CommitNodeResultAsync(Complete(oldLease), TestContext.Current.CancellationToken);
        var continued = await reopened.CommitNodeResultAsync(new CommitNodeResultCommand(
            newLease.WorkItemId, newLease.Token, newLease.Generation, newLease.InstanceRevision,
            NodeCommitKind.Continue, WorkflowState.Create("{\"step\":1}", 1),
            new NextWork(new TechnicalId("end"), newLease.StoreUtcNow), null), TestContext.Current.CancellationToken);
        var finalLease = Assert.IsType<WorkLease>((await reopened.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker.final"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
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
        var lease = Assert.IsType<WorkLease>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
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
        var lease = Assert.IsType<WorkLease>((await fixture.Store.ClaimDueWorkAsync(
            new ClaimDueWorkCommand(new TechnicalId("worker"), TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken)).Value);
        var conflict = await fixture.Store.CommitNodeResultAsync(
            Complete(lease) with { ExpectedInstanceRevision = lease.InstanceRevision + 1 },
            TestContext.Current.CancellationToken);
        var retried = await fixture.Store.CommitNodeResultAsync(Complete(lease), TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Conflict, conflict.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, retried.Outcome);
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

    private sealed record Fixture(string ConnectionString, OracleWorkflowDatabase Database, OracleWorkflowStore Store);

    private sealed class OptionalTextProbe
    {
        public string? BusinessKey { get; set; }
        public string? CorrelationId { get; set; }
    }
}

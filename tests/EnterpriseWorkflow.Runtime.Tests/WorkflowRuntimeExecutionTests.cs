using System.Collections.Concurrent;
using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class WorkflowRuntimeExecutionTests
{
    [Fact]
    public async Task StartServiceRetryEndAndOutboxRecoveryAreDurable()
    {
        await using var databaseFile = new TemporaryDatabase();
        var database = new SqliteWorkflowDatabase(databaseFile.ConnectionString);
        await database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = new SqliteWorkflowStore(database);
        var definition = BuildDefinition();
        Assert.Equal(StoreOutcome.Succeeded, (await store.PublishDefinitionAsync(
            new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(StoreOutcome.Succeeded, (await store.StartInstanceAsync(
            Start(definition), TestContext.Current.CancellationToken)).Outcome);

        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowStore>(store);
        services.AddSingleton<HandlerState>();
        services.AddSingleton<IWorkflowRetryDelayStrategy, ImmediateRetryDelay>();
        services.AddEnterpriseWorkflowHandlers(registry => registry.AddService<RetryThenSucceedHandler>("sample.execute", 1));
        services.AddEnterpriseWorkflowRuntime<DeduplicatingCrashOnceTransport>(options =>
        {
            options.LeaseDuration = TimeSpan.FromSeconds(5);
            options.LeaseRenewalInterval = TimeSpan.FromSeconds(1);
            options.IdlePollingInterval = TimeSpan.FromMilliseconds(10);
            options.RetryBaseDelay = TimeSpan.FromMilliseconds(1);
            options.RetryMaximumDelay = TimeSpan.FromMilliseconds(10);
            options.AttemptTimeout = TimeSpan.FromSeconds(10);
            options.OutboxLeaseDuration = TimeSpan.FromSeconds(5);
            options.OutboxIdlePollingInterval = TimeSpan.FromMilliseconds(10);
            options.OutboxMaximumAttempts = 4;
            options.OutboxRetryBaseDelay = TimeSpan.FromMilliseconds(1);
            options.OutboxRetryMaximumDelay = TimeSpan.FromMilliseconds(10);
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
        var owner = new TechnicalId("test.worker");

        Assert.True(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken)); // Start
        Assert.True(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken)); // Service attempt 1
        Assert.True(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken)); // Service attempt 2
        Assert.True(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken)); // End
        Assert.False(await pump.ExecuteNextAsync(owner, TestContext.Current.CancellationToken));

        var state = provider.GetRequiredService<HandlerState>();
        Assert.Equal([1, 2], state.Attempts);
        await using (var context = database.CreateDbContext())
        {
            var persisted = await context.Database.SqlQueryRaw<InstanceProbe>(
                "SELECT \"Status\", \"StateSchemaVersion\", \"StateJson\" FROM \"EwInstances\"")
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal((int)WorkflowInstanceStatus.Completed, persisted.Status);
            Assert.Equal(7, persisted.StateSchemaVersion);
            Assert.Equal("{\"processed\":true}", persisted.StateJson);
        }

        var transport = Assert.IsType<DeduplicatingCrashOnceTransport>(provider.GetRequiredService<IExternalEffectTransport>());
        var interrupted = Assert.IsType<OutboxLease>((await store.ClaimDueOutboxAsync(
            new ClaimDueOutboxCommand(new TechnicalId("test.crashed-outbox"), TimeSpan.FromMilliseconds(20)),
            TestContext.Current.CancellationToken)).Value);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await transport.DeliverAsync(interrupted, TestContext.Current.CancellationToken));
        await using (var context = database.CreateDbContext())
        {
            var expired = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "EwOutbox" SET "LeaseExpiresAtUnixMilliseconds" = 0 WHERE "Id" = {interrupted.MessageId.Value.ToString("N")}""",
                TestContext.Current.CancellationToken);
            Assert.Equal(1, expired);
        }

        var outbox = provider.GetRequiredService<IWorkflowOutboxPump>();
        Assert.True(await outbox.DeliverNextAsync(new TechnicalId("test.outbox"), TestContext.Current.CancellationToken));
        Assert.False(await outbox.DeliverNextAsync(new TechnicalId("test.outbox"), TestContext.Current.CancellationToken));
        var staleAcknowledgement = await store.CommitOutboxAsync(new CommitOutboxCommand(
            interrupted.MessageId, interrupted.Token, interrupted.Generation,
            OutboxCommitKind.Delivered, null, null), TestContext.Current.CancellationToken);
        Assert.Equal(StoreOutcome.Conflict, staleAcknowledgement.Outcome);
        Assert.Equal(2, transport.DeliveryAttempts);
        Assert.Single(transport.AppliedKeys);
    }

    [Fact]
    public void AllOperationalDefaultsCanBeOverriddenAndInvalidCombinationsFailFast()
    {
        var services = new ServiceCollection();
        services.AddEnterpriseWorkflowRuntime<DeduplicatingCrashOnceTransport>(options =>
        {
            options.WorkerConcurrency = 3;
            options.LeaseDuration = TimeSpan.FromSeconds(12);
            options.LeaseRenewalInterval = TimeSpan.FromSeconds(4);
            options.IdlePollingInterval = TimeSpan.FromSeconds(2);
            options.MaximumAttempts = 9;
            options.RetryBaseDelay = TimeSpan.FromSeconds(3);
            options.RetryMaximumDelay = TimeSpan.FromMinutes(2);
            options.AttemptTimeout = TimeSpan.FromMinutes(20);
            options.OutboxConcurrency = 2;
            options.OutboxMaximumAttempts = 17;
            options.OutboxLeaseDuration = TimeSpan.FromSeconds(40);
            options.OutboxIdlePollingInterval = TimeSpan.FromSeconds(3);
            options.OutboxRetryBaseDelay = TimeSpan.FromSeconds(5);
            options.OutboxRetryMaximumDelay = TimeSpan.FromMinutes(5);
        });

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddEnterpriseWorkflowRuntime<DeduplicatingCrashOnceTransport>(options =>
            {
                options.LeaseDuration = TimeSpan.FromSeconds(5);
                options.LeaseRenewalInterval = TimeSpan.FromSeconds(5);
            }));
    }

    [Fact]
    public async Task ExhaustedOutboxDeliveryMovesToFailedWithoutTouchingWorkflowState()
    {
        var lease = new OutboxLease(
            new OutboxMessageId(Guid.NewGuid()), new WorkflowInstanceId(Guid.NewGuid()),
            new NodeActivationId(Guid.NewGuid()), new TechnicalId("notify"), new TechnicalId("receiver"),
            "application/json", "{}", "stable-key", 3, 2, new LeaseToken(Guid.NewGuid()),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
        var store = new OutboxOnlyStore(lease);
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowStore>(store);
        services.AddEnterpriseWorkflowRuntime<AlwaysRetryTransport>(options => options.OutboxMaximumAttempts = 3);
        await using var provider = services.BuildServiceProvider();

        Assert.True(await provider.GetRequiredService<IWorkflowOutboxPump>().DeliverNextAsync(
            new TechnicalId("outbox.worker"), TestContext.Current.CancellationToken));

        Assert.NotNull(store.Committed);
        Assert.Equal(OutboxCommitKind.Fail, store.Committed!.Kind);
        Assert.Null(store.Committed.RetryAtUtc);
        Assert.Equal("receiver.unavailable", store.Committed.ErrorCode);
    }

    private static WorkflowDefinition BuildDefinition() => Assert.IsType<WorkflowDefinition>(
        WorkflowBuilder.Create("runtime.executable", 1)
            .Start("start")
            .Service("service", "sample.execute")
            .End("end")
            .Then("start", "service")
            .Then("service", "end")
            .Validate().Definition);

    private static StartInstanceCommand Start(WorkflowDefinition definition) => new(
        new StartCommandScope(new TechnicalId("installation.test"), new TechnicalId("workflow.start"),
            new ActorIdentity(new TechnicalId("local"), "runtime-test")),
        new TechnicalId("runtime-command"),
        "request-runtime",
        new DefinitionReference(definition.Id, definition.Version, definition.Sha256),
        WorkflowState.Create("{\"processed\":false}", 7),
        null,
        null,
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    private sealed class HandlerState
    {
        public ConcurrentQueue<int> Attempts { get; } = new();
    }

    private sealed class RetryThenSucceedHandler(HandlerState state) : IServiceNodeHandler
    {
        public ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
        {
            state.Attempts.Enqueue(context.Attempt);
            if (context.Attempt == 1)
                return ValueTask.FromResult(ServiceNodeResult.Retryable(new TechnicalId("sample.transient")));

            using var replacement = JsonDocument.Parse("{\"processed\":true}");
            var effect = new ExternalEffectIntent(
                new TechnicalId("notify"),
                new TechnicalId("sample.receiver"),
                "application/json",
                CanonicalJson.CreateObject("{\"event\":\"processed\"}", 1024));
            return ValueTask.FromResult(ServiceNodeResult.Success(
                NodeStateUpdate.Replace(replacement.RootElement), [effect]));
        }
    }

    private sealed class ImmediateRetryDelay : IWorkflowRetryDelayStrategy
    {
        public TimeSpan GetDelay(int completedAttempts, TimeSpan baseDelay, TimeSpan maximumDelay) => TimeSpan.Zero;
    }

    private sealed class DeduplicatingCrashOnceTransport : IExternalEffectTransport
    {
        private bool _crashed;
        public int DeliveryAttempts { get; private set; }
        public HashSet<string> AppliedKeys { get; } = new(StringComparer.Ordinal);

        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            DeliveryAttempts++;
            AppliedKeys.Add(message.IdempotencyKey);
            if (!_crashed)
            {
                _crashed = true;
                throw new InvalidOperationException("Simulated crash after the receiver applied the effect.");
            }

            return ValueTask.FromResult(ExternalEffectDeliveryResult.Success());
        }
    }

    private sealed class AlwaysRetryTransport : IExternalEffectTransport
    {
        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ExternalEffectDeliveryResult.Retryable("receiver.unavailable"));
    }

    private sealed class OutboxOnlyStore(OutboxLease lease) : IWorkflowStore
    {
        public CommitOutboxCommand? Committed { get; private set; }

        public ValueTask<StoreResult<OutboxLease>> ClaimDueOutboxAsync(
            ClaimDueOutboxCommand command, CancellationToken cancellationToken) =>
            ValueTask.FromResult(StoreResults.Succeeded(lease));

        public ValueTask<StoreResult<CommitOutboxResult>> CommitOutboxAsync(
            CommitOutboxCommand command, CancellationToken cancellationToken)
        {
            Committed = command;
            return ValueTask.FromResult(StoreResults.Succeeded(
                new CommitOutboxResult(OutboxMessageStatus.Failed, lease.Attempt)));
        }

        public ValueTask<StoreResult<DefinitionReference>> PublishDefinitionAsync(PublishDefinitionCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<StoreResult<StartInstanceResult>> StartInstanceAsync(StartInstanceCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<StoreResult<ClaimedWork>> ClaimDueWorkAsync(ClaimDueWorkCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<StoreResult<WorkLease>> RenewLeaseAsync(RenewLeaseCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<StoreResult<CommitNodeResult>> CommitNodeResultAsync(CommitNodeResultCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<StoreResult<CancelInstanceResult>> CancelInstanceAsync(CancelInstanceCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class InstanceProbe
    {
        public int Status { get; set; }
        public int StateSchemaVersion { get; set; }
        public string StateJson { get; set; } = string.Empty;
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"enterprise-workflow-runtime-{Guid.NewGuid():N}.db");
        public string ConnectionString => new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(_path);
            DeleteIfExists(_path + "-shm");
            DeleteIfExists(_path + "-wal");
            return ValueTask.CompletedTask;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

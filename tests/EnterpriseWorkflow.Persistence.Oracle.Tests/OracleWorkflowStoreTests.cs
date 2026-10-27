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
    public async Task TypedApprovalCommentsAreCommittedOnceWithTheDecision()
    {
        var fixture = await ResetDatabaseAsync(); var store = fixture.Store; var token = TestContext.Current.CancellationToken;
        var configuration = new WorkflowStagePolicy
        {
            StateContract = WorkflowStateContractCompiler.Compile<OracleApprovalState>(1), Permissions = [["oracle.act"]]
        }.ToConfigurationJson();
        var approval = new WorkflowStagePolicy
        {
            Permissions = [["oracle.act"]], Actions = new() { ["approve"] = new() { Label = "Approve", RequireComment = true, CommentPath = ["comments"] } }
        }.ToConfigurationJson();
        var definition = WorkflowBuilder.Create("oracle.typed", 1).Start("start", configuration)
            .HumanTask("review", HumanTaskKind.Approval, HumanTaskAssignmentMode.EligiblePool, "form", 1, "oracle.approve", [new("approve", "approve")], configurationJson: approval)
            .End("end").Then("start", "review").On("review", "approve", "end").Validate().Definition!;
        await store.PublishDefinitionAsync(new(definition), token);
        var started = await store.StartInstanceAsync(CreateStartCommand(definition, "typed-approval") with
        {
            InitialState = WorkflowState.Create("{\"step\":0,\"comments\":[]}", 1)
        }, token);
        Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
        var services = new ServiceCollection(); services.AddSingleton<IWorkflowStore>(store);
        services.AddSingleton<EnterpriseWorkflow.Security.IWorkflowAuthorizationService, OracleApprovalAuthorization>();
        services.AddEnterpriseWorkflowHandlers(registry => registry.AddHumanTask<StandardHumanTaskHandler>("oracle.approve", 1));
        services.AddEnterpriseWorkflowRuntime<OracleTestTransport>();
        await using var provider = services.BuildServiceProvider(); var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
        Assert.True(await pump.ExecuteNextAsync(new("worker"), token)); Assert.True(await pump.ExecuteNextAsync(new("worker"), token));
        var open = Assert.Single((await store.ReadOpenHumanTasksAsync(new(null, 10), token)).Value!.Tasks);
        var task = (await store.GetHumanTaskAsync(open.TaskId, token)).Value!; var human = provider.GetRequiredService<IHumanTaskService>();
        var actor = new EnterpriseWorkflow.Security.IdentityReference("local", "reviewer");
        Assert.True((await human.ClaimAsync(task.TaskId, task.Revision, actor, token)).Succeeded);
        task = (await store.GetHumanTaskAsync(task.TaskId, token)).Value!;
        Assert.Equal("runtime.comment-required", (await human.CompleteAsync(task.TaskId, task.Revision, actor, new("approve"), new("empty"), CanonicalJson.CreateObject("{}", 1024), token)).ErrorCode);
        var submission = CanonicalJson.CreateObject("{\"comment\":\"Oracle approval\"}", 1024);
        Assert.True((await human.CompleteAsync(task.TaskId, task.Revision, actor, new("approve"), new("approve-once"), submission, token)).Succeeded);
        Assert.True((await human.CompleteAsync(task.TaskId, task.Revision, actor, new("approve"), new("approve-once"), submission, token)).WasReplay);
        var state = (await store.ReadInstanceAsync(started.Value!.InstanceId, token)).Value!.State.Value.Deserialize<OracleApprovalState>();
        var comment = Assert.Single(state.Comments); Assert.Equal("Oracle approval", comment.Text); Assert.Equal("reviewer", comment.ActorSubjectId);
        var audit = Assert.Single((await store.SearchAuditAsync(new(Action: "approve", Stage: "review"), token)).Value!);
        Assert.Equal("reviewer", audit.Actor!.SubjectId);
        Assert.True(WorkflowPublicReference.IsValid(audit.Reference));
        Assert.Single((await store.SearchInstancesAsync(new(PublicReference: audit.Reference), token)).Value!);
    }
    public sealed class OracleApprovalState
    {
        [System.Text.Json.Serialization.JsonPropertyName("step")] public int Step { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("comments")] public List<WorkflowComment> Comments { get; init; } = [];
    }
    private sealed class OracleApprovalAuthorization : EnterpriseWorkflow.Security.IWorkflowAuthorizationService
    {
        public ValueTask<EnterpriseWorkflow.Security.AuthorizationDecision> AuthorizeAsync(EnterpriseWorkflow.Security.WorkflowAuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new EnterpriseWorkflow.Security.AuthorizationDecision(true, "test"));
    }

    [Fact]
    public async Task SearchMediaAndTatRebuildUseTheOracleTransactionBoundary()
    {
        var fixture = await ResetDatabaseAsync();
        var store = fixture.Store;
        var token = TestContext.Current.CancellationToken;
        var definition = BuildDefinition();
        await store.PublishDefinitionAsync(new(definition), token);
        var command = CreateStartCommand(definition, "l9b-media");
        var reference = new WorkflowMediaReference(Guid.NewGuid(), 1);
        var record = new WorkflowMediaRecord(reference, "oracle.txt", "text/plain", 4, new string('a', 64), "privatekey", command.Scope.Actor,
            null, null, command.IdempotencyKey.Value, "document", false, [], WorkflowMediaStatus.Staged, MillisecondUtcNow());
        record = record with { Status = WorkflowMediaStatus.Quarantined };
        Assert.Equal(StoreOutcome.Succeeded, (await store.StageMediaAsync(new(record), token)).Outcome);
        Assert.Equal(StoreOutcome.Conflict, (await store.StartInstanceAsync(command with { Media = [reference] }, token)).Outcome);
        Assert.Equal(StoreOutcome.Succeeded, (await store.CompleteMediaInspectionAsync(new(reference, record.Sha256, true), token)).Outcome);
        Assert.Equal(StoreOutcome.Conflict, (await store.StartInstanceAsync(command with { IdempotencyKey = new("wrong"), Media = [reference] }, token)).Outcome);
        Assert.Empty((await store.SearchInstancesAsync(new(), token)).Value!);
        var started = await store.StartInstanceAsync(command with { Media = [reference] }, token);
        Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
        await store.StartInstanceAsync(command with { Media = [reference] }, token);
        await store.StartInstanceAsync(command with { IdempotencyKey = new("second") }, token);
        var first = Assert.Single((await store.SearchInstancesAsync(new(Limit: 1), token)).Value!);
        var second = Assert.Single((await store.SearchInstancesAsync(new(Limit: 1, After: new(first.Summary.CreatedAtUtc.ToUnixTimeMilliseconds(), first.Summary.InstanceId.Value.ToString("N"))), token)).Value!);
        Assert.NotEqual(first.Summary.InstanceId, second.Summary.InstanceId);
        Assert.Equal(2, (await store.SearchInstancesAsync(new(Statuses: [first.Summary.Status]), token)).Value!.Count);
        Assert.Equal(definition.Sha256, (await store.ReadInstanceAsync(started.Value!.InstanceId, token)).Value!.Definition.Sha256);
        Assert.Single((await store.ListMediaAsync(started.Value.InstanceId, token)).Value!);
        Assert.Equal(StoreOutcome.Succeeded, (await store.RebuildTatAsync(started.Value.InstanceId, token)).Outcome);
        Assert.Equal(StoreOutcome.Succeeded, (await store.ReadTatAsync(new(), token)).Outcome);
        Assert.Equal(StoreOutcome.Succeeded, (await store.DeleteMediaAsync(new(reference, started.Value.InstanceId), token)).Outcome);
        Assert.Empty((await store.ListMediaAsync(started.Value.InstanceId, token)).Value!);
    }

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
        var owned = await reopened.ReadWorkflowInstancesAsync(
            new(command.Scope.Actor, 10), TestContext.Current.CancellationToken);
        var otherInitiator = await reopened.ReadWorkflowInstancesAsync(
            new(new ActorIdentity(new TechnicalId("local"), "another-subject"), 10),
            TestContext.Current.CancellationToken);
        var invalidLimit = await reopened.ReadWorkflowInstancesAsync(
            new(command.Scope.Actor, 0), TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, publication.Outcome);
        Assert.Equal(StoreOutcome.Idempotent, repeatedPublication.Outcome);
        Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
        Assert.Equal(StoreOutcome.Idempotent, repeatedStart.Outcome);
        Assert.Equal(started.Value!.InstanceId, repeatedStart.Value!.InstanceId);
        Assert.Equal(StoreOutcome.Conflict, conflict.Outcome);
        var summary = Assert.Single(owned.Value!);
        Assert.Equal(started.Value.InstanceId, summary.InstanceId);
        Assert.Equal(command.Definition.DefinitionId, summary.DefinitionId);
        Assert.Equal(command.Definition.Version, summary.DefinitionVersion);
        Assert.Empty(otherInitiator.Value!);
        Assert.Equal(StoreOutcome.Conflict, invalidLimit.Outcome);
    }

    [Fact]
    public async Task ModuleRemovalAndExplicitStateMigrationAreQualifiedOnOracle()
    {
        var fixture = await ResetDatabaseAsync();
        var artifact = new ModuleArtifactReference(new TechnicalId("fixture.approval"), "1.0.0", new string('a', 64));
        Assert.Equal(StoreOutcome.Succeeded, (await fixture.Store.ReconcileModuleArtifactsAsync(
            new ReconcileModuleArtifactsCommand([artifact]), TestContext.Current.CancellationToken)).Outcome);
        var definition = BuildDefinition(artifact);
        await fixture.Store.PublishDefinitionAsync(new PublishDefinitionCommand(definition), TestContext.Current.CancellationToken);
        var started = await fixture.Store.StartInstanceAsync(CreateStartCommand(definition, "migration-request"),
            TestContext.Current.CancellationToken);
        var snapshot = await fixture.Store.ReadStateMigrationSnapshotAsync(started.Value!.InstanceId,
            TestContext.Current.CancellationToken);
        var migrated = await fixture.Store.CommitStateMigrationAsync(new CommitStateMigrationCommand(
            started.Value.InstanceId, snapshot.Value!.Revision, 1,
            WorkflowState.Create("{\"migrated\":true}", 2), artifact,
            new ActorIdentity(new TechnicalId("local"), "migration-operator")),
            TestContext.Current.CancellationToken);
        var removed = await fixture.Store.ReconcileModuleArtifactsAsync(
            new ReconcileModuleArtifactsCommand([]), TestContext.Current.CancellationToken);
        var after = await fixture.Store.ReadStateMigrationSnapshotAsync(started.Value.InstanceId,
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreOutcome.Succeeded, migrated.Outcome);
        Assert.Equal((1L, 2), (migrated.Value!.Revision, migrated.Value.SchemaVersion));
        Assert.Equal(2, after.Value!.State.SchemaVersion);
        Assert.Equal("{\"migrated\":true}", after.Value.State.Value.CanonicalText);
        Assert.Equal((StoreOutcome.Conflict, "EW3051_REQUIRED_MODULE_ARTIFACT_MISSING"),
            (removed.Outcome, removed.ErrorCode));
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
        var report = await fixture.Store.ReadTatAsync(new(ByAnalyst: true), TestContext.Current.CancellationToken);
        Assert.Equal(StoreOutcome.Succeeded, report.Outcome);
        Assert.Equal(1, Assert.Single(report.Value!).CompletedCount);
        Assert.Equal(actor, report.Value![0].Analyst);
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

    [Fact]
    public async Task L8bResolvedAssignmentActivationLookupAndNotificationFanOutAreDurable()
    {
        var fixture = await ResetDatabaseAsync();
        var token = TestContext.Current.CancellationToken;
        var definition = WorkflowBuilder.Create("oracle.l8b", 1).Start("start")
            .HumanTask("confirmation", HumanTaskKind.Input, HumanTaskAssignmentMode.DesignatedIdentity, "form", 1, "handler", [new("submit", "submitted")])
            .End("end").Then("start", "confirmation").On("confirmation", "submitted", "end").Validate().Definition!;
        await fixture.Store.PublishDefinitionAsync(new(definition), token);
        var command = CreateStartCommand(definition, "l8b-request");
        await fixture.Store.StartInstanceAsync(command, token);
        var start = (await fixture.Store.ClaimDueWorkAsync(new(new("worker"), TimeSpan.FromSeconds(30)), token)).Value!;
        Assert.Equal(command.Scope.Actor, start.Initiator);
        await fixture.Store.CommitNodeResultAsync(new(start.Lease.WorkItemId, start.Lease.Token, start.Lease.Generation,
            start.Lease.InstanceRevision, NodeCommitKind.Continue, null, new(new("confirmation"), start.Lease.StoreUtcNow), null), token);
        var wait = (await fixture.Store.ClaimDueWorkAsync(new(new("worker"), TimeSpan.FromSeconds(30)), token)).Value!;
        var committed = await fixture.Store.CommitNodeResultAsync(new(wait.Lease.WorkItemId, wait.Lease.Token, wait.Lease.Generation,
            wait.Lease.InstanceRevision, NodeCommitKind.WaitHumanTask, null, null, null,
            [new(new("pool-notification"), new("workflow.pool-notification"), "application/json", "{}")],
            HumanTask: new(HumanTaskAssignmentMode.DesignatedIdentity, true, command.Scope.Actor)), token);
        Assert.Equal(StoreOutcome.Succeeded, committed.Outcome);
        var reopened = CreateStore(fixture.ConnectionString);
        var task = (await reopened.GetHumanTaskByActivationAsync(wait.Lease.ActivationId, token)).Value!;
        Assert.Equal(command.Scope.Actor, task.Assignee);
        Assert.Equal(HumanTaskStatus.Assigned, task.Status);
        var parent = (await reopened.ClaimDueOutboxAsync(new(new("mailer"), TimeSpan.FromSeconds(30)), token)).Value!;
        var fanOut = new CommitOutboxCommand(parent.MessageId, parent.Token, parent.Generation, OutboxCommitKind.Delivered, null, null,
            [new(new("recipient-1"), new("workflow.pool-mail"), "application/json", "{}")]);
        Assert.Equal(StoreOutcome.Succeeded, (await reopened.CommitOutboxAsync(fanOut, token)).Outcome);
        Assert.Equal(StoreOutcome.Conflict, (await reopened.CommitOutboxAsync(fanOut, token)).Outcome);
        var child = (await reopened.ClaimDueOutboxAsync(new(new("mailer"), TimeSpan.FromSeconds(30)), token)).Value!;
        Assert.Equal(parent.ActivationId, child.ActivationId);
        Assert.Equal("recipient-1", child.OperationId.Value);
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

    private static WorkflowDefinition BuildDefinition(ModuleArtifactReference? artifact = null)
    {
        var builder = WorkflowBuilder.Create("oracle.contract", 1);
        if (artifact is not null) builder.RequiresArtifact(artifact.Id.Value, artifact.Version, artifact.Sha256);
        var compilation = builder.Start("start").End("end").Then("start", "end").Validate();
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

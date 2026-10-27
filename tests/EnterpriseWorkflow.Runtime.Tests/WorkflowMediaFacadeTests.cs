using System.Text;
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

public sealed class WorkflowMediaFacadeTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ServiceAndHumanFileChangesCommitOnlyOnSuccess(bool accept, bool replace)
    {
        var root = Path.Combine(Path.GetTempPath(), "media-facade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var token = TestContext.Current.CancellationToken;
        try
        {
            var database = new SqliteWorkflowDatabase("Data Source=" + Path.Combine(root, "workflow.db"));
            await database.MigrateAsync(token);
            var store = new SqliteWorkflowStore(database);
            var policy = new WorkflowMediaPolicy("sample.documents", "Documents", 1024, ["text/plain"], [".txt"], false, []);
            var definition = WorkflowBuilder.Create("test", 1)
                .Start("start", new WorkflowStagePolicy { MediaPolicies = new() { [policy.Id] = policy } }.ToConfigurationJson())
                .Service("generate", "generate")
                .HumanTask("review", HumanTaskKind.Input, HumanTaskAssignmentMode.DesignatedIdentity, "form", 1, "review", [new("submit", "submit")],
                    allowInitiator: true, configurationJson: new WorkflowStagePolicy { AssignedToInitiator = true, Permissions = [["test.act"]], Actions = new() { ["submit"] = new() { Label = "Submit" } } }.ToConfigurationJson())
                .End("end").Then("start", "generate").Then("generate", "review").On("review", "submit", "end").Validate().Definition!;
            var published = await store.PublishDefinitionAsync(new(definition), token);
            var started = await store.StartInstanceAsync(new(new(new("installation"), new("workflow.start"), new(new("local"), "alice")),
                new("start"), "hash", published.Value!, WorkflowState.Create("{}", 1), null, null,
                DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())), token);
            Assert.Equal(StoreOutcome.Succeeded, started.Outcome);
            var state = new State { Accept = accept, Replace = replace };
            var services = new ServiceCollection();
            services.AddSingleton<IWorkflowStore>(store);
            services.AddSingleton(state);
            services.AddSingleton<IWorkflowAuthorizationService, Authorization>();
            services.AddEnterpriseWorkflowMedia(Path.Combine(root, "files"));
            services.AddEnterpriseWorkflowHandlers(registry => registry.AddService<Generate>("generate", 1).AddHumanTask<Review>("review", 1));
            services.AddEnterpriseWorkflowRuntime<Transport>();
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var pump = provider.GetRequiredService<IWorkflowExecutionPump>();
            for (var i = 0; i < 3; i++) Assert.True(await pump.ExecuteNextAsync(new("worker"), token));
            Assert.Equal("service.txt", Assert.Single((await store.ListMediaAsync(started.Value!.InstanceId, token)).Value!).Name);
            var open = Assert.Single((await store.ReadOpenHumanTasksAsync(new(null, 10), token)).Value!.Tasks);
            var task = (await store.GetHumanTaskAsync(open.TaskId, token)).Value!;
            var completed = await provider.GetRequiredService<IHumanTaskService>().CompleteAsync(task.TaskId, task.Revision, new("local", "alice"),
                new("submit"), new("complete"), CanonicalJson.CreateObject("{}", 100), token);
            Assert.Equal(accept, completed.Succeeded);
            if (!accept) { Assert.Equal(2, completed.Errors!.Count); Assert.Equal("documents", completed.Errors[0].Field); }
            var active = Assert.Single((await store.ListMediaAsync(started.Value.InstanceId, token)).Value!);
            Assert.Equal(accept ? replace ? "corrected.txt" : "human.txt" : "service.txt", active.Name);
            Assert.Equal(accept ? replace ? WorkflowMediaStatus.Superseded : WorkflowMediaStatus.Deleted : WorkflowMediaStatus.Active, (await store.ReadMediaAsync(state.ServiceFile!, token)).Value!.Status);
            Assert.Throws<InvalidOperationException>(() => state.Session!.Bind(provider.GetRequiredService<IWorkflowMediaService>()));
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }

    [Fact]
    public void ModulePoliciesAreSharedAcrossProcessesAndRequireExplicitVisibility()
    {
        var module = new WorkflowModuleBuilder(new("sample", "1.0.0", 1));
        module.MediaPolicies(policies => policies.Add("documents").AllowFormats(MediaFormats.Text).MaximumSize(100).VisibleToInstanceReaders());
        module.AddHumanTask<Review>("review").AddForm("form", "Form", form => form.HandledBy<Review>()
            .Fields(fields => fields.File("documents", "Documents").MaximumFiles(2).UseMediaPolicy("documents")));
        foreach (var id in new[] { "one", "two" }) module.AddProcess(process => process.WithState<EmptyState>().InitializedBy<EmptyInitializer>().WithId(id).WithStateSchemaVersion(1).RequiresPermission("act").StartWithForm("form").Then("end").End("end"));
        var permissions = new WorkflowPermissionRegistryBuilder(module.Info).Add("act", "Act");
        var registry = new EnterpriseWorkflow.Presentation.WorkflowPresentationRegistryBuilder();
        module.ConfigurePresentation(registry, permissions.Permissions);
        Assert.Equal(2, registry.Processes.Count);
        foreach (var process in registry.Processes)
        {
            var definition = process.DefinitionFactory(new(new("sample"), "1.0.0", new string('a', 64)));
            var configuration = WorkflowStagePolicy.Read(definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start).Configuration)!;
            Assert.Equal("sample", configuration.MediaPolicyModule);
            var policy = Assert.Single(configuration.MediaPolicies!);
            Assert.Equal("sample.documents", policy.Key);
        }
        var invalid = new WorkflowModuleBuilder(new("invalid", "1.0.0", 1));
        invalid.MediaPolicies(policies => policies.Add("documents").AllowFormats(MediaFormats.Text).MaximumSize(100));
        Assert.Throws<InvalidOperationException>(() => invalid.ConfigurePresentation(new(), []));
    }
    [Fact]
    public async Task UploadedFieldIsCollectedAndForeignOwnerIsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "media-field-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var token = TestContext.Current.CancellationToken;
        try
        {
            var database = new SqliteWorkflowDatabase("Data Source=" + Path.Combine(root, "workflow.db"));
            await database.MigrateAsync(token);
            var store = new SqliteWorkflowStore(database);
            var media = new WorkflowMediaService(store, new FileWorkflowMediaStorageProvider(Path.Combine(root, "files")), []);
            var actor = new ActorIdentity(new("local"), "alice");
            var policy = new WorkflowMediaPolicy("sample.documents", "Documents", 1024, ["text/plain"], [".txt"], false, []);
            var config = new WorkflowStagePolicy { MediaPolicies = new() { [policy.Id] = policy }, MediaFields = new() { ["documents"] = policy.Id } }.ToConfigurationJson();
            var definition = WorkflowBuilder.Create("form", 1).Start("start", config).End("end").Then("start", "end").Validate().Definition!;
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Upload"));
            var upload = await media.StageAsync(new(actor, null, null, "submission", "documents"), "upload", "file.txt", stream, new(1024, ["text/plain"], [".txt"]), new(false, []), token);
            var submission = CanonicalJson.CreateObject(System.Text.Json.JsonSerializer.Serialize(new { documents = new[] { upload.Reference } }), 4096);
            var execution = WorkflowMediaSession.Create(definition, definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start).Configuration, new(actor, null, null, "submission"), submission);
            var context = new WorkflowStateInitializationContext(actor, submission, "submission") { MediaExecution = execution };
            Assert.Equal(upload.Reference, Assert.Single(await media.For(context).FromFieldAsync("documents", token)).Reference);
            Assert.Equal(upload.Reference, Assert.Single(execution.Seal().Attachments));
            var foreignActor = new ActorIdentity(new("local"), "bob");
            var foreign = new WorkflowStateInitializationContext(foreignActor, submission, "submission")
            { MediaExecution = WorkflowMediaSession.Create(definition, definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start).Configuration, new(foreignActor, null, null, "submission"), submission) };
            await Assert.ThrowsAsync<InvalidDataException>(async () => await media.For(foreign).FromFieldAsync("documents", token));
            Assert.Empty(foreign.MediaExecution.Seal().Attachments);
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }

    public sealed class EmptyState;
    private sealed class EmptyInitializer : IWorkflowStateInitializer
    {
        public ValueTask<WorkflowStateInitializationResult> InitializeAsync(WorkflowStateInitializationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(new WorkflowStateInitializationResult(CanonicalJson.CreateObject("{}", 1024)));
    }

    private sealed class State { public bool Accept { get; init; } public bool Replace { get; init; } public WorkflowMediaReference? ServiceFile { get; set; } public WorkflowMediaSession? Session { get; set; } }
    private sealed class Generate(IWorkflowMediaService media, State state) : IServiceNodeHandler
    {
        public async ValueTask<ServiceNodeResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("service"));
            state.ServiceFile = (await media.For(context).StoreAsync("service", "service.txt", input, "documents", cancellationToken)).Reference;
            return ServiceNodeResult.Success();
        }
    }
    private sealed class Review(IWorkflowMediaService media, State state) : IHumanTaskCompletionHandler
    {
        public async ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken)
        {
            var files = media.For(context); state.Session = files;
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("human"));
            if (state.Replace) await files.ReplaceAsync(state.ServiceFile!, "corrected", "corrected.txt", input, "documents", cancellationToken);
            else
            {
                await files.DeleteAsync(state.ServiceFile!, cancellationToken);
                await files.StoreAsync("human", "human.txt", input, "documents", cancellationToken);
            }
            return state.Accept ? HumanTaskCompletionResult.Success() : HumanTaskCompletionResult.Rejected(new WorkflowValidationError("sample.invalid-file", "Le document est invalide.", "documents"), new WorkflowValidationError("sample.review", "Une vérification supplémentaire est nécessaire."));
        }
    }
    private sealed class Authorization : IWorkflowAuthorizationService
    {
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(true, "test"));
    }
    private sealed class Transport : IExternalEffectTransport
    {
        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken) => ValueTask.FromResult(ExternalEffectDeliveryResult.Success());
    }
}

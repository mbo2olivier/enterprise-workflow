using EnterpriseWorkflow.Core.Model;
using System.Reflection;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Extensions.Tests;

public sealed class LeaveRequestModuleTests
{
    [Fact]
    public async Task ExactArtifactDeclaresVersionedProcessFormComponentAndDesignatedApproval()
    {
        var root = Path.Combine(Path.GetTempPath(), $"enterprise-workflow-leave-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "dependencies"));
            var repository = FindRepositoryRoot();
            var source = Path.Combine(repository, "samples", "LeaveRequest.Module", "bin",
                BuildConfiguration(), "net10.0", "EnterpriseWorkflow.LeaveRequest.Module.dll");
            var assembly = Path.Combine(root, "EnterpriseWorkflow.LeaveRequest.Module.dll");
            File.Copy(source, assembly);
            var sdkSource = Path.Combine(repository, "src", "EnterpriseWorkflow.Sdk", "bin",
                BuildConfiguration(), "net10.0", "EnterpriseWorkflow.Sdk.dll");
            var sdk = Path.Combine(root, "dependencies", "EnterpriseWorkflow.Sdk.dll");
            File.Copy(sdkSource, sdk);
            var moduleType = System.Reflection.Assembly.LoadFrom(source).GetType("EnterpriseWorkflow.LeaveRequest.LeaveRequestModule", throwOnError: true)!;
            var authoringModule = (WorkflowModule)Activator.CreateInstance(moduleType)!;
            var manifest = WorkflowModuleManifestWriter.Create(authoringModule, assembly, [sdk]);
            await File.WriteAllTextAsync(Path.Combine(root, "module.json"), manifest, TestContext.Current.CancellationToken);

            var moduleCatalog = WorkflowModuleCatalog.Load([root]);
            var services = new ServiceCollection();
            services.AddEnterpriseWorkflowModules(moduleCatalog);
            services.AddSingleton<EnterpriseWorkflow.Core.Serialization.IWorkflowSerializer>(new EnterpriseWorkflow.Core.Serialization.WorkflowSerializer());
            services.AddSingleton(DispatchProxy.Create<IWorkflowMediaService, UnusedMedia>());
            await using var provider = services.BuildServiceProvider();
            var catalog = provider.GetRequiredService<IWorkflowPresentationCatalog>();

            var process = Assert.Single(catalog.Processes);
            Assert.Equal("sample.leave-request.leave-request", process.Definition.Id.Value);
            Assert.Empty(process.DesignatedAssignmentNodes);
            Assert.Equal(4, catalog.Permissions.Length);
            Assert.Equal(9, process.Definition.Version);
            var draft = process.Definition.Nodes.Single(node => node.Id.Value == "draft");
            var manager = process.Definition.Nodes.Single(node => node.Id.Value == "validation-manager");
            var hr = process.Definition.Nodes.Single(node => node.Id.Value == "validation-rh");
            Assert.Equal("validation-manager", WorkflowStagePolicy.Read(draft.Configuration)!.Actions["submit"].AssigneeTarget);
            Assert.Null(WorkflowStagePolicy.Read(draft.Configuration)!.Actions["cancel"].AssigneeTarget);
            Assert.True(WorkflowStagePolicy.Read(manager.Configuration)!.AssignedFromPreviousTask);
            Assert.Equal(HumanTaskAssignmentMode.EligiblePool, hr.HumanTask!.AssignmentMode);
            Assert.Null(WorkflowStagePolicy.Read(hr.Configuration)!.Actions["validate"].AssigneeTarget);
            AssertTransition("draft", "submit", "validation-manager");
            AssertTransition("validation-manager", "validate", "validation-rh");
            AssertTransition("validation-rh", "validate", "close-request");
            AssertTransition("validation-manager", "decline", "rejected");
            AssertTransition("validation-rh", "decline", "rejected");
            AssertTransition("draft", "cancel", "cancelled");

            void AssertTransition(string source, string action, string target)
            {
                var node = process.Definition.Nodes.Single(node => node.Id.Value == source);
                var outcome = node.HumanTask!.Actions.Single(item => item.ActionId.Value == action).Outcome;
                Assert.Contains(process.Definition.Transitions, edge => edge.SourceId.Value == source && edge.Outcome == outcome && edge.TargetId.Value == target);
            }
            Assert.True(catalog.TryResolveForm(process.Definition, process.StartForm, out var form));
            Assert.NotNull(form!.CustomComponentType);
            Assert.Equal("LeaveRequestSummary", form.CustomComponentType.Name);
            var initializer = (IWorkflowStateInitializer)ActivatorUtilities.GetServiceOrCreateInstance(provider, process.StateInitializerType!);
            var actor = new EnterpriseWorkflow.Persistence.ActorIdentity(new("local"), "alice");
            var submission = CanonicalJson.CreateObject("""{"startDate":"2026-11-02","endDate":"2026-11-04","leaveType":"paid","reason":"Qualification","privateNote":"hidden","documents":[]}""", 256 * 1024);
            var mediaExecution = WorkflowMediaSession.Create(process.Definition, process.Definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start).Configuration,
                new(actor, null, null, "test"), submission);
            var initialized = await initializer.InitializeAsync(new(actor, submission, "test") { MediaExecution = mediaExecution }, TestContext.Current.CancellationToken);
            Assert.Empty(mediaExecution.Seal().Attachments);
            var contract = WorkflowStateContract.From(process.Definition)!;
            Assert.Empty(contract.Validate(initialized.State));
            Assert.True(await provider.GetRequiredService<IWorkflowStateValidationService>().ValidateAsync(process.Definition,
                new(WorkflowState.Create(initialized.State.CanonicalText, process.InitialStateSchemaVersion), "start", null, []), TestContext.Current.CancellationToken));
            var invalid = WorkflowState.Create(initialized.State.CanonicalText.Replace("2026-11-04", "2026-11-01", StringComparison.Ordinal), process.InitialStateSchemaVersion);
            var errors = await provider.GetRequiredService<IWorkflowStateValidationService>().ValidateErrorsAsync(process.Definition,
                new(invalid, "start", null, []), TestContext.Current.CancellationToken);
            var error = Assert.Single(errors);
            Assert.Equal("leave.invalid-dates", error.Code);
            Assert.Contains("date de fin", error.Message, StringComparison.Ordinal);
            Assert.Null(error.Field);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) when (OperatingSystem.IsWindows()) { }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows()) { }
        }
    }

    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
        ?? throw new InvalidOperationException("Could not determine the build configuration.");

    public class UnusedMedia : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException("Empty uploads must not access storage.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EnterpriseWorkflow.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}

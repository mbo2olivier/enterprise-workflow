using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Sdk;
using Microsoft.Extensions.Options;
using Xunit;

namespace EnterpriseWorkflow.Presentation.Tests;

public sealed class WorkflowStartVersionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LatestVersionIsSelectedBeforeAuthorizationAndOldDirectStartsAreRejected(bool allowLatest)
    {
        var catalog = new Catalog();
        var permissions = new Permissions(allowLatest);
        var portal = new WorkflowPortalService(catalog, permissions, null!, null!, null!, null!, permissions, null!,
            new Dictionary<string, IIdentityDirectory>(), Options.Create(new WorkflowPortalOptions()));
        var actor = new IdentityReference("local", "alice");
        var cards = await portal.ListStartableProcessesAsync(actor, TestContext.Current.CancellationToken);
        if (allowLatest) Assert.Equal(2, Assert.Single(cards).Version);
        else Assert.Empty(cards);
        var result = await portal.StartAsync(actor, new("leave", 1, "{}", new("test"), new Dictionary<EnterpriseWorkflow.Abstractions.TechnicalId, IdentityReference>()), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Equal("presentation.process-version-obsolete", result.ErrorCode);
        Assert.True(catalog.TryGetProcess("leave", 1, out _)); // Historical definitions stay available.
    }
    public sealed class EmptyState;
    private sealed class Initializer : EnterpriseWorkflow.Runtime.IWorkflowStateInitializer
    {
        public ValueTask<EnterpriseWorkflow.Runtime.WorkflowStateInitializationResult> InitializeAsync(EnterpriseWorkflow.Runtime.WorkflowStateInitializationContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new EnterpriseWorkflow.Runtime.WorkflowStateInitializationResult(CanonicalJson.CreateObject("{}", 1024)));
    }
    private sealed class Permissions(bool allowLatest) : IAuthorizationProvider, IWorkflowAuthorizationService
    {
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(true, "test"));
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AuthorizationDecision(allowLatest || request.Scope.DefinitionVersion == 1, "test"));
    }
    private sealed class Catalog : IWorkflowPresentationCatalog
    {
        private static readonly WorkflowArtifactReference Artifact = new(new("module"), "1.0.0", new string('a', 64));
        private static readonly WorkflowFormDefinition Form = new(new(new("start"), 1), "Start", []);
        public ImmutableArray<WorkflowProcessDescriptor> Processes { get; } = [Create(1), Create(2)];
        private static WorkflowProcessDescriptor Create(int version) => new(WorkflowBuilder.Create("leave", version).Start("start", new WorkflowStagePolicy { StateContract = WorkflowStateContractCompiler.Compile<EmptyState>(1) }.ToConfigurationJson()).End("end").Then("start", "end").Validate().Definition!, Artifact, "Congé", "", Form.Reference, 1, [], null, typeof(Initializer));
        public bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process) { process = Processes.FirstOrDefault(p => p.Definition.Id.Value == id && p.Definition.Version == version); return process is not null; }
        public bool TryResolveForm(WorkflowDefinition definition, WorkflowFormReference reference, out WorkflowFormRegistration? registration) { registration = new(Artifact, Form, null); return true; }
    }
}

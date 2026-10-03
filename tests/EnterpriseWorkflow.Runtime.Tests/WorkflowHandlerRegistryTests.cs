using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Runtime.Tests;

public sealed class WorkflowHandlerRegistryTests
{
    [Fact]
    public void DuplicateGlobalKeyIsRejectedDuringConfiguration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<WorkflowHandlerRegistrationException>(() =>
            services.AddEnterpriseWorkflowHandlers(registry => registry
                .AddService<ScopedServiceHandler>("finance.post", 1)
                .AddService<OtherServiceHandler>("finance.post", 1)));

        Assert.Equal("EW4001_DUPLICATE_HANDLER", exception.Code);
        Assert.Empty(services);
    }

    [Fact]
    public void RegistryIsOrdinalVersionedAndImmutable()
    {
        var builder = new WorkflowHandlerRegistryBuilder()
            .AddService<ScopedServiceHandler>("finance.post", 2)
            .AddService<OtherServiceHandler>("finance.post", 1)
            .AddDecision<DecisionHandler>("finance.route", 1);
        var registry = builder.Build();

        Assert.Equal(
            ["finance.post:1", "finance.post:2", "finance.route:1"],
            registry.Handlers.Select(item => $"{item.Reference.Id.Value}:{item.Reference.Version}"));
        Assert.True(registry.TryGet(Reference("finance.post", 2), out var descriptor));
        Assert.Equal(typeof(ScopedServiceHandler), descriptor!.HandlerType);
        Assert.False(registry.TryGet(Reference("FINANCE.POST", 2), out _));
    }

    [Fact]
    public void RegistryCannotBeConfiguredTwice()
    {
        var services = new ServiceCollection();
        services.AddEnterpriseWorkflowHandlers(registry => registry
            .AddService<ScopedServiceHandler>("finance.post", 1));

        var exception = Assert.Throws<WorkflowHandlerRegistrationException>(() =>
            services.AddEnterpriseWorkflowHandlers(registry => registry
                .AddDecision<DecisionHandler>("finance.route", 1)));

        Assert.Equal("EW4005_REGISTRY_ALREADY_CONFIGURED", exception.Code);
    }

    [Fact]
    public void MissingAndIncompatibleBindingsPreventPublication()
    {
        var definition = Assert.IsType<WorkflowDefinition>(WorkflowBuilder.Create("binding.test", 1)
            .Start("start")
            .Service("service", "finance.post")
            .Decision("decision", "finance.route", ["approved"])
            .End("end")
            .Then("start", "service")
            .Then("service", "decision")
            .On("decision", "approved", "end")
            .Validate().Definition);
        var registry = new WorkflowHandlerRegistryBuilder()
            .AddDecision<DecisionHandler>("finance.post", 1)
            .Build();

        var result = WorkflowDefinitionBindingValidator.Validate(definition, registry);
        var exception = Assert.Throws<WorkflowBindingValidationException>(result.EnsureValid);

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Diagnostics,
            diagnostic => Assert.Equal("EW4002_HANDLER_NOT_FOUND", diagnostic.Code),
            diagnostic => Assert.Equal("EW4003_HANDLER_KIND_MISMATCH", diagnostic.Code));
        Assert.Equal(result.Diagnostics, exception.Diagnostics);
    }

    [Fact]
    public void FullyBoundDefinitionPassesPublicationGate()
    {
        var definition = Assert.IsType<WorkflowDefinition>(WorkflowBuilder.Create("binding.valid", 1)
            .Start("start")
            .Service("service", "finance.post")
            .End("end")
            .Then("start", "service")
            .Then("service", "end")
            .Validate().Definition);
        var registry = new WorkflowHandlerRegistryBuilder()
            .AddService<ScopedServiceHandler>("finance.post", 1)
            .Build();

        var result = WorkflowDefinitionBindingValidator.Validate(definition, registry);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
        result.EnsureValid();
    }

    [Fact]
    public async Task ResolverCreatesAndDisposesOneScopePerAttempt()
    {
        var services = new ServiceCollection();
        services.AddScoped<AttemptDependency>();
        services.AddEnterpriseWorkflowHandlers(registry => registry
            .AddService<ScopedServiceHandler>("finance.post", 1));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var resolver = provider.GetRequiredService<IScopedWorkflowHandlerResolver>();

        Guid firstDependency;
        AttemptDependency firstObject;
        await using (var first = resolver.ResolveService(Reference("finance.post", 1)))
        {
            var handler = Assert.IsType<ScopedServiceHandler>(first.Handler);
            firstDependency = handler.Dependency.Id;
            firstObject = handler.Dependency;
        }

        await using (var second = resolver.ResolveService(Reference("finance.post", 1)))
        {
            var handler = Assert.IsType<ScopedServiceHandler>(second.Handler);
            Assert.NotEqual(firstDependency, handler.Dependency.Id);
        }

        Assert.True(firstObject.IsDisposed);
    }

    [Fact]
    public void ResolverNeverFallsBackToAnotherVersionOrKind()
    {
        var services = new ServiceCollection();
        services.AddEnterpriseWorkflowHandlers(registry => registry
            .AddService<ScopedServiceHandler>("finance.post", 1));
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IScopedWorkflowHandlerResolver>();

        var missing = Assert.Throws<WorkflowHandlerResolutionException>(() =>
            resolver.ResolveService(Reference("finance.post", 2)));
        var wrongKind = Assert.Throws<WorkflowHandlerResolutionException>(() =>
            resolver.ResolveDecision(Reference("finance.post", 1)));

        Assert.Equal("EW4002_HANDLER_NOT_FOUND", missing.Code);
        Assert.Equal("EW4003_HANDLER_KIND_MISMATCH", wrongKind.Code);
    }

    [Fact]
    public async Task ResultContractsKeepTransitionsAndRetryPolicyOutsideHandlers()
    {
        var context = new NodeExecutionContext(
            new EnterpriseWorkflow.Persistence.WorkflowInstanceId(Guid.NewGuid()),
            new EnterpriseWorkflow.Persistence.NodeActivationId(Guid.NewGuid()),
            new TechnicalId("service"),
            1,
            WorkflowState.Create("{\"amount\":10}", 1),
            CanonicalJson.CreateObject("{\"currency\":\"EUR\"}", 1024));
        IServiceNodeHandler service = new ScopedServiceHandler(new AttemptDependency());
        IDecisionNodeHandler decision = new DecisionHandler();

        var serviceResult = await service.ExecuteAsync(context, TestContext.Current.CancellationToken);
        var decisionResult = await decision.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(NodeExecutionDisposition.Succeeded, serviceResult.Disposition);
        Assert.Null(serviceResult.ErrorCode);
        Assert.Equal(new TechnicalId("approved"), decisionResult.Outcome);
        Assert.Equal(NodeExecutionDisposition.Succeeded, decisionResult.Disposition);
    }

    [Fact]
    public void ExecutionContextRejectsEmptyDurableIdentityAndAttempt()
    {
        var state = WorkflowState.Create("{}", 1);
        var configuration = CanonicalJson.CreateObject("{}", 1024);

        Assert.Throws<ArgumentException>(() => new NodeExecutionContext(
            new EnterpriseWorkflow.Persistence.WorkflowInstanceId(Guid.Empty),
            new EnterpriseWorkflow.Persistence.NodeActivationId(Guid.NewGuid()),
            new TechnicalId("service"),
            1,
            state,
            configuration));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NodeExecutionContext(
            new EnterpriseWorkflow.Persistence.WorkflowInstanceId(Guid.NewGuid()),
            new EnterpriseWorkflow.Persistence.NodeActivationId(Guid.NewGuid()),
            new TechnicalId("service"),
            0,
            state,
            configuration));
    }

    [Fact]
    public void ResultFactoriesRejectDefaultTechnicalIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => ServiceNodeResult.Retryable(default));
        Assert.Throws<ArgumentException>(() => DecisionNodeResult.Select(default));
        Assert.Throws<ArgumentException>(() => DecisionNodeResult.PermanentFailure(default));
    }

    private static WorkflowHandlerReference Reference(string id, int version) =>
        new(new TechnicalId(id), version);

    private sealed class AttemptDependency : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class ScopedServiceHandler(AttemptDependency dependency) : IServiceNodeHandler
    {
        public AttemptDependency Dependency { get; } = dependency;

        public ValueTask<ServiceNodeResult> ExecuteAsync(
            NodeExecutionContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(ServiceNodeResult.Success());
    }

    private sealed class OtherServiceHandler : IServiceNodeHandler
    {
        public ValueTask<ServiceNodeResult> ExecuteAsync(
            NodeExecutionContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(ServiceNodeResult.Success());
    }

    private sealed class DecisionHandler : IDecisionNodeHandler
    {
        public ValueTask<DecisionNodeResult> EvaluateAsync(
            NodeExecutionContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(DecisionNodeResult.Select(new TechnicalId("approved")));
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Registers the durable workflow worker and transactional outbox dispatcher.</summary>
public static class WorkflowRuntimeServiceCollectionExtensions
{
    /// <summary>Registers configurable workers and one singleton transport implementation.</summary>
    public static IServiceCollection AddEnterpriseWorkflowRuntime<TTransport>(
        this IServiceCollection services,
        Action<WorkflowRuntimeOptions>? configure = null)
        where TTransport : class, IExternalEffectTransport
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new WorkflowRuntimeOptions();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IWorkflowRetryDelayStrategy, FullJitterWorkflowRetryDelayStrategy>();
        services.AddSingleton<IOptions<WorkflowRuntimeOptions>>(Options.Create(options));
        services.TryAddSingleton<IWorkflowExecutionPump, WorkflowExecutionPump>();
        services.TryAddSingleton<IWorkflowOutboxPump, WorkflowOutboxPump>();
        services.TryAddSingleton<IWorkflowTimerPump, WorkflowTimerPump>();
        services.TryAddSingleton<IWorkflowStartService>(provider => new WorkflowStartService(
            provider.GetRequiredService<EnterpriseWorkflow.Persistence.IWorkflowStore>(),
            provider.GetRequiredService<EnterpriseWorkflow.Security.IWorkflowAuthorizationService>()));
        services.TryAddSingleton<IHumanTaskService>(provider => new HumanTaskService(
            provider.GetRequiredService<EnterpriseWorkflow.Persistence.IWorkflowStore>(),
            provider.GetRequiredService<EnterpriseWorkflow.Security.IWorkflowAuthorizationService>(),
            provider.GetRequiredService<IScopedWorkflowHandlerResolver>(),
            provider.GetRequiredService<IOptions<WorkflowRuntimeOptions>>()));
        services.TryAddSingleton<IExternalEffectTransport, TTransport>();
        services.AddHostedService<WorkflowWorkerHostedService>();
        services.AddHostedService<WorkflowOutboxHostedService>();
        services.AddHostedService<WorkflowTimerHostedService>();
        return services;
    }
}

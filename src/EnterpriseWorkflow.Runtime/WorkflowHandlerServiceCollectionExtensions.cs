using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Registers one immutable L4a handler registry and scoped resolver.</summary>
public static class WorkflowHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Configures all direct application handlers, rejects duplicate keys immediately and freezes the registry.
    /// </summary>
    public static IServiceCollection AddEnterpriseWorkflowHandlers(
        this IServiceCollection services,
        Action<WorkflowHandlerRegistryBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(item => item.ServiceType == typeof(IWorkflowHandlerRegistry)))
        {
            throw new WorkflowHandlerRegistrationException(
                "EW4005_REGISTRY_ALREADY_CONFIGURED",
                "The immutable workflow handler registry has already been configured.");
        }

        var builder = new WorkflowHandlerRegistryBuilder();
        configure(builder);
        var descriptors = builder.BuildDescriptors();
        var registry = new WorkflowHandlerRegistry(descriptors);
        services.AddSingleton<IWorkflowHandlerRegistry>(registry);
        services.AddSingleton<IScopedWorkflowHandlerResolver, ScopedWorkflowHandlerResolver>();

        foreach (var handlerType in descriptors.Select(item => item.HandlerType).Distinct())
        {
            services.AddScoped(handlerType);
        }

        return services;
    }
}

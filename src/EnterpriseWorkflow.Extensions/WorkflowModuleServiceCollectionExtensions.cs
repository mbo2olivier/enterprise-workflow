using EnterpriseWorkflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Extensions;

/// <summary>Composes trusted startup modules into the existing immutable L4a handler registry.</summary>
public static class WorkflowModuleServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseWorkflowModules(
        this IServiceCollection services,
        WorkflowModuleCatalog catalog,
        Action<WorkflowHandlerRegistryBuilder>? configureHostHandlers = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var module in catalog.Modules) module.EntryPoint.ConfigureServices(services);
        return services.AddEnterpriseWorkflowHandlers(handlers =>
        {
            configureHostHandlers?.Invoke(handlers);
            foreach (var module in catalog.Modules) module.EntryPoint.ConfigureHandlers(handlers);
        });
    }
}

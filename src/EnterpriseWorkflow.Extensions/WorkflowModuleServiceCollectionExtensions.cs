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
        var migrations = WorkflowStateMigrationRegistry.Create(catalog);
        var presentation = WorkflowPresentationCatalog.Create(catalog);
        foreach (var module in catalog.Modules) module.EntryPoint.ConfigureServices(services);
        foreach (var migration in migrations.Registrations) services.AddTransient(migration.MigratorType);
        services.AddSingleton(catalog);
        services.AddSingleton<IWorkflowStateMigrationRegistry>(migrations);
        services.AddSingleton<IWorkflowPresentationCatalog>(presentation);
        services.AddScoped<WorkflowStateMigrationService>();
        return services.AddEnterpriseWorkflowHandlers(handlers =>
        {
            configureHostHandlers?.Invoke(handlers);
            foreach (var module in catalog.Modules) module.EntryPoint.ConfigureHandlers(handlers);
        });
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Enables media in embedded/headless hosts as well as Web hosts.</summary>
public static class WorkflowMediaServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseWorkflowMedia(this IServiceCollection services, string privateRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateRoot);
        services.TryAddSingleton<IWorkflowMediaStorageProvider>(_ => new FileWorkflowMediaStorageProvider(privateRoot));
        services.TryAddSingleton<IWorkflowMediaService, WorkflowMediaService>();
        return services;
    }
}

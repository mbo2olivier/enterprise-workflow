using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Presentation;

public static class WorkflowPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseWorkflowPresentation(
        this IServiceCollection services,
        Action<WorkflowPortalOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new WorkflowPortalOptions();
        configure?.Invoke(options);
        options.Validate();
        services.AddSingleton<IOptions<WorkflowPortalOptions>>(Options.Create(options));
        services.AddSingleton<IWorkflowFormService, WorkflowFormService>();
        services.AddSingleton<IWorkflowPortalService, WorkflowPortalService>();
        return services;
    }
}

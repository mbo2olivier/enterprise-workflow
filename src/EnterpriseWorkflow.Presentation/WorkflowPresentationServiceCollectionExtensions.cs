using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using EnterpriseWorkflow.Runtime;

namespace EnterpriseWorkflow.Presentation;

public static class WorkflowPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseWorkflowPresentation(
        this IServiceCollection services,
        Action<WorkflowPortalOptions>? configure = null,
        Action<WorkflowAuditOptions>? configureAudit = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new WorkflowPortalOptions();
        configure?.Invoke(options);
        options.Validate();
        services.AddSingleton<IOptions<WorkflowPortalOptions>>(Options.Create(options));
        services.AddEnterpriseWorkflowMedia(options.MediaRoot);
        services.AddSingleton<WorkflowMediaPortalService>();
        var auditOptions = new WorkflowAuditOptions();
        configureAudit?.Invoke(auditOptions);
        auditOptions.Validate();
        services.AddSingleton<IOptions<WorkflowAuditOptions>>(Options.Create(auditOptions));
        services.AddSingleton<EnterpriseWorkflow.Security.IIdentityDisplayNameResolver, IdentityDisplayNameResolver>();
        services.AddSingleton<WorkflowAuditService>();
        services.AddSingleton<WorkflowTatService>();
        services.AddSingleton<IWorkflowFormService, WorkflowFormService>();
        services.AddSingleton<IWorkflowPortalService, WorkflowPortalService>();
        return services;
    }
}

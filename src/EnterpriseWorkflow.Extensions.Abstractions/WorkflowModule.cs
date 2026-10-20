using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Extensions;

/// <summary>Fluent authoring entry point; the existing startup interface remains supported.</summary>
public abstract class WorkflowModule : IWorkflowModule
{
    private readonly Lazy<WorkflowModuleBuilder> _declarations;
    protected WorkflowModule() => _declarations = new(() =>
    {
        var builder = new WorkflowModuleBuilder(Info);
        Configure(builder);
        return builder;
    });
    public abstract WorkflowModuleInfo Info { get; }
    public IReadOnlyDictionary<string, Type> Components => _declarations.Value.Components;
    protected abstract void Configure(WorkflowModuleBuilder builder);
    public virtual void ConfigurePermissions(WorkflowPermissionRegistryBuilder permissions) { }
    public virtual void ConfigureServices(IServiceCollection services) { }
    public virtual void ConfigureStateMigrations(WorkflowStateMigrationRegistryBuilder migrations) { }
    public void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers) => _declarations.Value.ConfigureHandlers(handlers);
    public void ConfigurePresentation(WorkflowPresentationRegistryBuilder presentation)
    {
        var permissions = new WorkflowPermissionRegistryBuilder(Info);
        ConfigurePermissions(permissions);
        _declarations.Value.ConfigurePresentation(presentation, permissions.Permissions);
    }
}

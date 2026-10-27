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
        var builder = new WorkflowModuleBuilder(EffectiveInfo);
        Configure(builder);
        return builder;
    });
    public abstract WorkflowModuleInfo Info { get; }
    private WorkflowModuleInfo? _developmentInfo;
    internal WorkflowModuleInfo EffectiveInfo => _developmentInfo ?? Info;
    WorkflowModuleInfo? IWorkflowModule.Info => EffectiveInfo;
    internal void SetDevelopmentGeneration(int generation)
    {
        if (_declarations.IsValueCreated || _developmentInfo is not null) throw new InvalidOperationException("Module declarations have already been materialized.");
        if (generation is < 1 or > 1000000000) throw new ArgumentOutOfRangeException(nameof(generation));
        var release = Info.ReleaseVersion.Split('+')[0];
        release += (release.Contains('-', StringComparison.Ordinal) ? "." : "-") + "ewdev.g" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var info = new WorkflowModuleInfo(Info.Id.Value, release, checked(1000000000 + generation));
        if (Info.Name is { } name) info = info.WithName(name);
        if (Info.Description is { } description) info = info.WithDescription(description);
        _developmentInfo = info;
    }
    public IReadOnlyDictionary<string, Type> Components => _declarations.Value.Components;
    protected abstract void Configure(WorkflowModuleBuilder builder);
    public virtual void ConfigurePermissions(WorkflowPermissionRegistryBuilder permissions) { }
    public virtual void ConfigureServices(IServiceCollection services) { }
    public virtual void ConfigureStateMigrations(WorkflowStateMigrationRegistryBuilder migrations) { }
    public void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers) => _declarations.Value.ConfigureHandlers(handlers);
    public void ConfigurePresentation(WorkflowPresentationRegistryBuilder presentation)
    {
        var permissions = new WorkflowPermissionRegistryBuilder(EffectiveInfo);
        ConfigurePermissions(permissions);
        _declarations.Value.ConfigurePresentation(presentation, permissions.Permissions);
    }
}

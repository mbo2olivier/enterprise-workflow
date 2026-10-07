using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Extensions;

/// <summary>
/// Startup contract implemented by a trusted module entry point.
/// Modules execute with the privileges of the host process and are not a security sandbox.
/// </summary>
public interface IWorkflowModule
{
    /// <summary>Registers module-owned services before the immutable service provider is built.</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Adds exact, globally versioned handlers to the shared L4a registry.</summary>
    void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers);

    /// <summary>Adds explicit single-step state-schema migrations owned by this exact module artifact.</summary>
    void ConfigureStateMigrations(WorkflowStateMigrationRegistryBuilder migrations);

    /// <summary>Adds exact process and form declarations owned by this module artifact.</summary>
    void ConfigurePresentation(WorkflowPresentationRegistryBuilder presentation);
}

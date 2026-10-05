using EnterpriseWorkflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Extensions;

/// <summary>
/// Experimental startup contract implemented by a trusted module entry point.
/// Modules execute with the privileges of the host process and are not a security sandbox.
/// </summary>
public interface IWorkflowModule
{
    /// <summary>Registers module-owned services before the immutable service provider is built.</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Adds exact, globally versioned handlers to the shared L4a registry.</summary>
    void ConfigureHandlers(WorkflowHandlerRegistryBuilder handlers);
}

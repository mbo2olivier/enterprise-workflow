using EnterpriseWorkflow.Extensions;
using Microsoft.Extensions.Configuration;

namespace EnterpriseWorkflow.Hosting;

/// <summary>Selects discovery exclusively when RootDirectory is configured; otherwise uses the legacy explicit list.</summary>
public static class WorkflowModuleSource
{
    public static WorkflowModuleCatalog Load(IConfiguration configuration, string contentRootPath,
        Action<WorkflowModuleLoadOptions>? configure = null,
        Action<WorkflowModuleDiscoveryOptions>? configureDiscovery = null,
        Action<WorkflowModuleDiscoveryDiagnostic>? reportDiagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var modules = configuration.GetSection("Modules");
        var rootConfigured = modules.GetChildren().Any(section => string.Equals(section.Key, "RootDirectory", StringComparison.OrdinalIgnoreCase));
        if (rootConfigured)
        {
            var root = modules["RootDirectory"];
            if (string.IsNullOrWhiteSpace(root))
                throw new WorkflowModuleLoadException("EW7060_INVALID_DISCOVERY_ROOT", "Modules:RootDirectory is configured but empty. Individual module paths are ignored.");
            string fullRoot;
            try { fullRoot = Path.GetFullPath(root, contentRootPath); }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException) { throw new WorkflowModuleLoadException("EW7060_INVALID_DISCOVERY_ROOT", "Modules:RootDirectory is invalid.", exception); }
            reportDiagnostic?.Invoke(new("EW7064_MODULE_DISCOVERY_SELECTED", fullRoot, "Discovery root selected; Modules:Directories is ignored."));
            return WorkflowModuleCatalog.Discover(fullRoot, configure, options =>
            {
                modules.GetSection("Discovery").Bind(options);
                configureDiscovery?.Invoke(options);
                var callback = options.ReportDiagnostic;
                options.ReportDiagnostic = diagnostic => { callback?.Invoke(diagnostic); reportDiagnostic?.Invoke(diagnostic); };
            });
        }
        reportDiagnostic?.Invoke(new("EW7065_EXPLICIT_MODULES_SELECTED", contentRootPath, "No discovery root configured; using Modules:Directories."));
        // Preserve legacy relative-path resolution as well as its explicit loading semantics.
        return WorkflowModuleCatalog.Load(modules.GetSection("Directories").Get<string[]>() ?? [], configure);
    }
}

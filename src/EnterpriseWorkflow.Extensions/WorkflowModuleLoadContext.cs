using System.Reflection;
using System.Runtime.Loader;

namespace EnterpriseWorkflow.Extensions;

internal sealed class WorkflowModuleLoadContext : AssemblyLoadContext
{
    private readonly Dictionary<string, Assembly> _sharedAssemblies;
    private readonly Dictionary<string, string> _privateAssemblies;

    internal WorkflowModuleLoadContext(
        WorkflowModuleManifest manifest,
        IEnumerable<ParsedDependency> dependencies,
        IEnumerable<Assembly> sharedAssemblies)
        : base($"EnterpriseWorkflow.Module:{manifest.Id.Value}:{manifest.Version}", isCollectible: false)
    {
        _sharedAssemblies = sharedAssemblies
            .Where(item => item.GetName().Name is not null)
            .GroupBy(item => item.GetName().Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        try
        {
            _privateAssemblies = dependencies.ToDictionary(item =>
            {
                try
                {
                    return AssemblyName.GetAssemblyName(item.FullPath).Name
                        ?? throw new BadImageFormatException("Assembly has no simple name.");
                }
                catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
                {
                    throw new WorkflowModuleLoadException("EW7010_INVALID_MODULE_DEPENDENCY",
                        $"Dependency '{item.Model.Path}' is not a managed .NET assembly.", exception);
                }
            }, item => item.FullPath, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException exception)
        {
            throw new WorkflowModuleLoadException("EW7011_DUPLICATE_DEPENDENCY_ASSEMBLY",
                "Several declared dependencies use the same assembly simple name.", exception);
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && _sharedAssemblies.TryGetValue(assemblyName.Name, out var shared))
            return shared;
        if (assemblyName.Name is not null && _privateAssemblies.TryGetValue(assemblyName.Name, out var path))
            return LoadFromAssemblyPath(path);
        return null;
    }
}

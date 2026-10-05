using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Components;

namespace EnterpriseWorkflow.Extensions;

/// <summary>
/// Experimental startup-only catalog. Each configured artifact is loaded once into a dedicated,
/// non-collectible context and retained until process exit.
/// </summary>
public sealed class WorkflowModuleCatalog
{
    private readonly ImmutableArray<LoadedWorkflowModule> _modules;
    private readonly ImmutableDictionary<ResourceKey, ParsedResource> _resources;

    private WorkflowModuleCatalog(
        ImmutableArray<LoadedWorkflowModule> modules,
        ImmutableDictionary<ResourceKey, ParsedResource> resources)
    {
        _modules = modules;
        _resources = resources;
    }

    public ImmutableArray<LoadedWorkflowModule> Modules => _modules;

    public static WorkflowModuleCatalog Load(
        IEnumerable<string> configuredModuleDirectories,
        Action<WorkflowModuleLoadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuredModuleDirectories);
        var options = new WorkflowModuleLoadOptions();
        configure?.Invoke(options);
        options.Validate();
        var artifacts = configuredModuleDirectories
            .Select(path => WorkflowModuleManifestReader.Read(path, options))
            .OrderBy(item => item.Manifest.Id.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Manifest.Version, StringComparer.Ordinal)
            .ToArray();
        var duplicate = artifacts.GroupBy(item => (item.Manifest.Id.Value, item.Manifest.Version))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new WorkflowModuleLoadException("EW7012_DUPLICATE_MODULE_VERSION",
                $"Module '{duplicate.Key.Value}' version '{duplicate.Key.Version}' is configured more than once.");

        var shared = SharedAssemblies();
        var loaded = ImmutableArray.CreateBuilder<LoadedWorkflowModule>(artifacts.Length);
        var resources = ImmutableDictionary.CreateBuilder<ResourceKey, ParsedResource>();
        foreach (var artifact in artifacts)
        {
            var context = new WorkflowModuleLoadContext(artifact.Manifest, artifact.Dependencies, shared);
            try
            {
                var assembly = context.LoadFromAssemblyPath(artifact.EntryAssemblyPath);
                var entryType = assembly.GetType(artifact.Manifest.EntryType, throwOnError: false, ignoreCase: false);
                if (entryType is null || entryType.IsAbstract || !entryType.IsPublic ||
                    !typeof(IWorkflowModule).IsAssignableFrom(entryType) || entryType.GetConstructor(Type.EmptyTypes) is null)
                    throw new WorkflowModuleLoadException("EW7013_INVALID_MODULE_ENTRY_POINT",
                        $"Entry type '{artifact.Manifest.EntryType}' must be public, concrete, implement IWorkflowModule, and have a public parameterless constructor.");
                var components = artifact.Manifest.Components.Select(component =>
                {
                    var type = assembly.GetType(component.TypeName, throwOnError: false, ignoreCase: false);
                    if (type is null || type.IsAbstract || !type.IsPublic || !typeof(IComponent).IsAssignableFrom(type))
                        throw new WorkflowModuleLoadException("EW7014_INVALID_MODULE_COMPONENT",
                            $"Component type '{component.TypeName}' must be a public concrete Blazor component.");
                    return new WorkflowModuleComponent(component.Id, component.TypeName, type);
                }).ToImmutableArray();
                var entryPoint = (IWorkflowModule)Activator.CreateInstance(entryType)!;
                var module = new LoadedWorkflowModule(artifact.RootPath, artifact.Manifest, artifact.ArtifactSha256,
                    assembly, entryPoint, components);
                loaded.Add(module);
                foreach (var resource in artifact.Resources)
                {
                    resources.Add(new ResourceKey(module.Manifest.Id.Value, module.Manifest.Version,
                        module.ArtifactSha256, resource.Model.Path), resource);
                }
            }
            catch (WorkflowModuleLoadException)
            {
                throw;
            }
            catch (Exception exception) when (exception is FileLoadException or BadImageFormatException or TypeLoadException or TargetInvocationException)
            {
                throw new WorkflowModuleLoadException("EW7015_MODULE_ACTIVATION_FAILED",
                    $"Module '{artifact.Manifest.Id.Value}' version '{artifact.Manifest.Version}' could not be activated.", exception);
            }
        }
        return new WorkflowModuleCatalog(loaded.MoveToImmutable(), resources.ToImmutable());
    }

    public bool TryOpenResource(
        string moduleId,
        string version,
        string artifactSha256,
        string path,
        out WorkflowModuleResource? resource,
        out Stream? content)
    {
        if (_resources.TryGetValue(new ResourceKey(moduleId, version, artifactSha256, path), out var parsed))
        {
            resource = parsed.Model;
            content = new FileStream(parsed.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return true;
        }
        resource = null;
        content = null;
        return false;
    }

    private static Assembly[] SharedAssemblies()
    {
        _ = typeof(IWorkflowModule);
        _ = typeof(global::EnterpriseWorkflow.Runtime.IWorkflowHandlerRegistry);
        _ = typeof(global::EnterpriseWorkflow.Runtime.IServiceNodeHandler);
        _ = typeof(global::EnterpriseWorkflow.Core.Model.WorkflowDefinition);
        _ = typeof(global::EnterpriseWorkflow.Abstractions.TechnicalId);
        _ = typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection);
        _ = typeof(IComponent);
        return AssemblyLoadContext.Default.Assemblies
            .Where(assembly => assembly.GetName().Name is { } name &&
                (name.StartsWith("EnterpriseWorkflow.", StringComparison.Ordinal) ||
                 name is "Microsoft.Extensions.DependencyInjection.Abstractions" or "Microsoft.AspNetCore.Components"))
            .ToArray();
    }

    private readonly record struct ResourceKey(string ModuleId, string Version, string ArtifactSha256, string Path);
}

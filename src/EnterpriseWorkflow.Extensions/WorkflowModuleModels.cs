using System.Collections.Immutable;
using System.Reflection;
using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Extensions;

/// <summary>Strict immutable description of one declared module resource.</summary>
public sealed record WorkflowModuleResource(string Path, string ContentType, string Sha256, long Length);

/// <summary>One Razor component resolved from a versioned module assembly.</summary>
public sealed record WorkflowModuleComponent(TechnicalId Id, string TypeName, Type ComponentType);

/// <summary>One dependency whose bytes were verified before any module code was loaded.</summary>
public sealed record WorkflowModuleDependency(string Path, string Sha256);

/// <summary>Validated manifest data retained after loading.</summary>
public sealed record WorkflowModuleManifest(
    int SchemaVersion,
    TechnicalId Id,
    string Version,
    int ContractVersion,
    string EntryAssembly,
    string EntryType,
    string EntryAssemblySha256,
    ImmutableArray<WorkflowModuleDependency> Dependencies,
    ImmutableArray<WorkflowModuleResource> Resources,
    ImmutableArray<(TechnicalId Id, string TypeName)> Components);

/// <summary>A trusted module loaded into its own non-collectible context for the process lifetime.</summary>
public sealed class LoadedWorkflowModule
{
    internal LoadedWorkflowModule(
        string rootPath,
        WorkflowModuleManifest manifest,
        string artifactSha256,
        Assembly entryAssembly,
        IWorkflowModule entryPoint,
        ImmutableArray<WorkflowModuleComponent> components)
    {
        RootPath = rootPath;
        Manifest = manifest;
        ArtifactSha256 = artifactSha256;
        EntryAssembly = entryAssembly;
        EntryPoint = entryPoint;
        Components = components;
    }

    public string RootPath { get; }
    public WorkflowModuleManifest Manifest { get; }
    public string ArtifactSha256 { get; }
    public Assembly EntryAssembly { get; }
    public IWorkflowModule EntryPoint { get; }
    public ImmutableArray<WorkflowModuleComponent> Components { get; }
}

/// <summary>Stable coded failure raised before the host is considered ready.</summary>
public sealed class WorkflowModuleLoadException : InvalidOperationException
{
    public WorkflowModuleLoadException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException) => Code = code;

    public string Code { get; }
}

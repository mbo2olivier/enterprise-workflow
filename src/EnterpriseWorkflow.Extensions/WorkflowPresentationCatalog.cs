using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Sdk;
using EnterpriseWorkflow.Runtime;

namespace EnterpriseWorkflow.Extensions;

public sealed record WorkflowFormRegistration(
    WorkflowArtifactReference Artifact,
    WorkflowFormDefinition Form,
    Type? CustomComponentType);

public interface IWorkflowPresentationCatalog
{
    ImmutableArray<WorkflowProcessDescriptor> Processes { get; }
    ImmutableArray<WorkflowPermissionDefinition> Permissions => [];
    bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process);
    bool TryResolveForm(WorkflowDefinition definition, WorkflowFormReference reference,
        out WorkflowFormRegistration? registration);
}

/// <summary>Immutable startup catalog of presentation declarations owned by exact module artifacts.</summary>
public sealed class WorkflowPresentationCatalog : IWorkflowPresentationCatalog
{
    private readonly ImmutableDictionary<ProcessKey, WorkflowProcessDescriptor> _processes;
    private readonly ImmutableDictionary<FormKey, WorkflowFormRegistration> _forms;

    public ImmutableArray<WorkflowPermissionDefinition> Permissions { get; private init; } = [];

    private WorkflowPresentationCatalog(
        ImmutableDictionary<ProcessKey, WorkflowProcessDescriptor> processes,
        ImmutableDictionary<FormKey, WorkflowFormRegistration> forms)
    {
        _processes = processes;
        _forms = forms;
    }

    public ImmutableArray<WorkflowProcessDescriptor> Processes => _processes.Values
        .OrderBy(item => item.Definition.Id.Value, StringComparer.Ordinal)
        .ThenBy(item => item.Definition.Version)
        .ToImmutableArray();

    public bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process) =>
        _processes.TryGetValue(new ProcessKey(id, version), out process);

    public bool TryResolveForm(
        WorkflowDefinition definition,
        WorkflowFormReference reference,
        out WorkflowFormRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reference);
        registration = null;
        foreach (var artifact in definition.Artifacts)
        {
            if (!_forms.TryGetValue(new FormKey(artifact.Id.Value, artifact.Version, artifact.Sha256,
                    reference.Id.Value, reference.Version), out var candidate))
                continue;
            if (registration is not null)
                throw new WorkflowModuleLoadException("EW7025_AMBIGUOUS_FORM_REFERENCE",
                    $"Form '{reference.Id.Value}' version {reference.Version} is provided by more than one artifact referenced by workflow '{definition.Id.Value}' version {definition.Version}.");
            registration = candidate;
        }
        return registration is not null;
    }

    internal static WorkflowPresentationCatalog Create(WorkflowModuleCatalog catalog)
    {
        var processes = ImmutableDictionary.CreateBuilder<ProcessKey, WorkflowProcessDescriptor>();
        var forms = ImmutableDictionary.CreateBuilder<FormKey, WorkflowFormRegistration>();
        var handlerOwners = new Dictionary<WorkflowHandlerReference, WorkflowArtifactReference>();
        var handlers = new WorkflowHandlerRegistryBuilder();
        foreach (var owner in catalog.Modules)
        {
            var owned = new WorkflowHandlerRegistryBuilder();
            owner.EntryPoint.ConfigureHandlers(owned);
            foreach (var descriptor in owned.Build().Handlers)
            {
                if (!handlerOwners.TryAdd(descriptor.Reference, new(owner.Manifest.Id, owner.Manifest.Version, owner.ArtifactSha256)))
                    throw new WorkflowModuleLoadException("EW7033_DUPLICATE_HANDLER_OWNER", "A durable handler key has multiple owners.");
            }
            owner.EntryPoint.ConfigureHandlers(handlers);
        }
        var handlerRegistry = handlers.Build();
        var permissions = new Dictionary<string, WorkflowPermissionDefinition>(StringComparer.Ordinal);
        var declarations = new List<(LoadedWorkflowModule Module, WorkflowPresentationRegistryBuilder Builder, WorkflowArtifactReference Artifact)>();
        foreach (var module in catalog.Modules)
        {
            if (module.EntryPoint.Info is { } metadata)
            {
                var permissionBuilder = new WorkflowPermissionRegistryBuilder(metadata);
                module.EntryPoint.ConfigurePermissions(permissionBuilder);
                foreach (var permission in permissionBuilder.Permissions) permissions[permission.Id.Value] = permission;
            }
            var builder = new WorkflowPresentationRegistryBuilder();
            try
            {
                module.EntryPoint.ConfigurePresentation(builder);
            }
            catch (Exception exception) when (exception is not WorkflowModuleLoadException)
            {
                throw new WorkflowModuleLoadException("EW7022_INVALID_PRESENTATION_DECLARATION",
                    $"Module '{module.Manifest.Id.Value}' version '{module.Manifest.Version}' declared invalid presentation metadata.", exception);
            }

            var artifact = new WorkflowArtifactReference(
                module.Manifest.Id, module.Manifest.Version, module.ArtifactSha256);
            foreach (var form in builder.Forms)
            {
                Type? componentType = null;
                if (form.CustomComponentId is { } componentId)
                {
                    componentType = module.Components.SingleOrDefault(item => item.Id == componentId)?.ComponentType;
                    if (componentType is null)
                        throw new WorkflowModuleLoadException("EW7023_FORM_COMPONENT_NOT_DECLARED",
                            $"Form '{form.Reference.Id.Value}' version {form.Reference.Version} references undeclared component '{componentId.Value}'.");
                }
                var key = new FormKey(artifact.Id.Value, artifact.Version, artifact.Sha256,
                    form.Reference.Id.Value, form.Reference.Version);
                forms.Add(key, new WorkflowFormRegistration(artifact, form, componentType));
            }

            declarations.Add((module, builder, artifact));
        }

        foreach (var (module, builder, artifact) in declarations)
        {
            foreach (var (reference, owner) in builder.ReferencedHandlerOwners)
                if (!handlerOwners.TryGetValue(reference, out var actualOwner) || actualOwner != owner)
                    throw new WorkflowModuleLoadException("EW7037_IMPORTED_HANDLER_OWNER_MISMATCH", "Imported handler does not belong to the specified exact artifact.");
            foreach (var (reference, owner) in builder.ReferencedFormOwners)
                if (!forms.ContainsKey(new FormKey(owner.Id.Value, owner.Version, owner.Sha256, reference.Id.Value, reference.Version)))
                    throw new WorkflowModuleLoadException("EW7038_IMPORTED_FORM_OWNER_MISMATCH", "Imported form does not belong to the specified exact artifact.");
            foreach (var declaration in builder.Processes)
            {
                WorkflowDefinition definition;
                try
                {
                    definition = declaration.DefinitionFactory(artifact);
                }
                catch (Exception exception) when (exception is not WorkflowModuleLoadException)
                {
                    throw new WorkflowModuleLoadException("EW7024_PROCESS_DEFINITION_FACTORY_FAILED",
                        $"Process '{declaration.Id.Value}' version {declaration.Version} could not build its exact definition.", exception);
                }
                if (definition.Id != declaration.Id || definition.Version != declaration.Version ||
                    (!builder.IsReferencedProcess(declaration.Id.Value, declaration.Version) && !definition.Artifacts.Contains(artifact)))
                    throw new WorkflowModuleLoadException("EW7026_PROCESS_DEFINITION_IDENTITY_MISMATCH",
                        $"Process '{declaration.Id.Value}' version {declaration.Version} must build the same definition identity and reference its owning exact artifact.");
                if (WorkflowStagePolicy.Read(definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start).Configuration) is not null)
                {
                    WorkflowDefinitionBindingValidator.Validate(definition, handlerRegistry).EnsureValid();
                    foreach (var policy in definition.Nodes.Select(node => WorkflowStagePolicy.Read(node.Configuration)).OfType<WorkflowStagePolicy>())
                    {
                        var required = policy.Permissions.Concat(policy.ReadPermissions).Concat(policy.CancelPermissions)
                            .Concat(policy.Actions.Values.SelectMany(action => action.Permissions)).SelectMany(group => group);
                        if (required.Any(permission => !permissions.ContainsKey(permission)))
                            throw new WorkflowModuleLoadException("EW7036_UNKNOWN_PERMISSION", "A definition references an undeclared module permission.");
                    }
                    foreach (var node in definition.Nodes)
                    {
                        var reference = node.Handler ?? node.HumanTask?.CompletionHandler;
                        if (reference is not null && (!handlerOwners.TryGetValue(reference, out var owner) || !definition.Artifacts.Contains(owner)))
                            throw new WorkflowModuleLoadException("EW7034_HANDLER_ARTIFACT_MISSING", $"Handler '{reference}' has no exact owner in the definition.");
                        if (node.HumanTask is { } task)
                        {
                            var matches = definition.Artifacts.Count(owner => forms.ContainsKey(new FormKey(owner.Id.Value, owner.Version, owner.Sha256, task.Form.Id.Value, task.Form.Version)));
                            if (matches != 1) throw new WorkflowModuleLoadException("EW7035_TASK_FORM_REFERENCE_INVALID", $"Task '{node.Id}' must resolve exactly one form owner.");
                        }
                    }
                }
                foreach (var nodeId in declaration.DesignatedAssignmentNodes)
                {
                    var node = definition.Nodes.SingleOrDefault(item => item.Id == nodeId);
                    if (node?.HumanTask is not { AssignmentMode: HumanTaskAssignmentMode.DesignatedIdentity })
                        throw new WorkflowModuleLoadException("EW7027_INVALID_DESIGNATED_ASSIGNMENT_NODE",
                            $"Process '{declaration.Id.Value}' designates node '{nodeId.Value}', which is not a designated-identity human task.");
                }
                foreach (var dependency in definition.Artifacts)
                {
                    if (!catalog.Modules.Any(owner => owner.Manifest.Id == dependency.Id && owner.Manifest.Version == dependency.Version && owner.ArtifactSha256 == dependency.Sha256))
                        throw new WorkflowModuleLoadException("EW7030_REQUIRED_ARTIFACT_MISSING", $"Exact artifact '{dependency.Id}/{dependency.Version}' is required.");
                }
                if (!definition.Artifacts.Any(owner => forms.ContainsKey(new FormKey(owner.Id.Value, owner.Version, owner.Sha256,
                        declaration.StartForm.Id.Value, declaration.StartForm.Version))))
                    throw new WorkflowModuleLoadException("EW7028_PROCESS_START_FORM_NOT_FOUND",
                        $"Process '{declaration.Id.Value}' start form is not owned by the same artifact.");
                var processOwner = builder.ReferencedProcessOwner(declaration.Id.Value, declaration.Version) ?? artifact;
                if (!definition.Artifacts.Contains(processOwner))
                    throw new WorkflowModuleLoadException("EW7032_PROCESS_OWNER_MISMATCH", "The reused definition does not reference its original owner.");
                var descriptor = new WorkflowProcessDescriptor(definition, processOwner, declaration.Title,
                    declaration.Description, declaration.StartForm, declaration.InitialStateSchemaVersion,
                    declaration.DesignatedAssignmentNodes, declaration.IconResourcePath);
                var processKey = new ProcessKey(definition.Id.Value, definition.Version);
                if (processes.TryGetValue(processKey, out var existing) && existing.Definition.Sha256 == definition.Sha256 &&
                    builder.IsReferencedProcess(declaration.Id.Value, declaration.Version)) continue;
                if (processes.TryGetValue(processKey, out existing) && existing.Definition.Sha256 == definition.Sha256 &&
                    declarations.Any(item => item.Builder.IsReferencedProcess(declaration.Id.Value, declaration.Version)))
                {
                    processes[processKey] = descriptor;
                    continue;
                }
                if (!processes.TryAdd(processKey, descriptor))
                    throw new WorkflowModuleLoadException("EW7029_DUPLICATE_PROCESS_VERSION",
                        $"Process '{definition.Id.Value}' version {definition.Version} is declared by more than one module.");
            }
        }
        return new WorkflowPresentationCatalog(processes.ToImmutable(), forms.ToImmutable())
        {
            Permissions = permissions.Values.OrderBy(permission => permission.Id.Value, StringComparer.Ordinal).ToImmutableArray(),
        };
    }

    private readonly record struct ProcessKey(string Id, int Version);
    private readonly record struct FormKey(
        string ModuleId, string ModuleVersion, string ArtifactSha256, string FormId, int FormVersion);
}

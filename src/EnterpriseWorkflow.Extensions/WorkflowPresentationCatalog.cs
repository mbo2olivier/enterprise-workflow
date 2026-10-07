using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;

namespace EnterpriseWorkflow.Extensions;

public sealed record WorkflowFormRegistration(
    WorkflowArtifactReference Artifact,
    WorkflowFormDefinition Form,
    Type? CustomComponentType);

public interface IWorkflowPresentationCatalog
{
    ImmutableArray<WorkflowProcessDescriptor> Processes { get; }
    bool TryGetProcess(string id, int version, out WorkflowProcessDescriptor? process);
    bool TryResolveForm(WorkflowDefinition definition, WorkflowFormReference reference,
        out WorkflowFormRegistration? registration);
}

/// <summary>Immutable startup catalog of presentation declarations owned by exact module artifacts.</summary>
public sealed class WorkflowPresentationCatalog : IWorkflowPresentationCatalog
{
    private readonly ImmutableDictionary<ProcessKey, WorkflowProcessDescriptor> _processes;
    private readonly ImmutableDictionary<FormKey, WorkflowFormRegistration> _forms;

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
        foreach (var module in catalog.Modules)
        {
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
                    !definition.Artifacts.Contains(artifact))
                    throw new WorkflowModuleLoadException("EW7026_PROCESS_DEFINITION_IDENTITY_MISMATCH",
                        $"Process '{declaration.Id.Value}' version {declaration.Version} must build the same definition identity and reference its owning exact artifact.");
                foreach (var nodeId in declaration.DesignatedAssignmentNodes)
                {
                    var node = definition.Nodes.SingleOrDefault(item => item.Id == nodeId);
                    if (node?.HumanTask is not { AssignmentMode: HumanTaskAssignmentMode.DesignatedIdentity })
                        throw new WorkflowModuleLoadException("EW7027_INVALID_DESIGNATED_ASSIGNMENT_NODE",
                            $"Process '{declaration.Id.Value}' designates node '{nodeId.Value}', which is not a designated-identity human task.");
                }
                var startFormKey = new FormKey(artifact.Id.Value, artifact.Version, artifact.Sha256,
                    declaration.StartForm.Id.Value, declaration.StartForm.Version);
                if (!forms.ContainsKey(startFormKey))
                    throw new WorkflowModuleLoadException("EW7028_PROCESS_START_FORM_NOT_FOUND",
                        $"Process '{declaration.Id.Value}' start form is not owned by the same artifact.");
                var descriptor = new WorkflowProcessDescriptor(definition, artifact, declaration.Title,
                    declaration.Description, declaration.StartForm, declaration.InitialStateSchemaVersion,
                    declaration.DesignatedAssignmentNodes, declaration.IconResourcePath);
                if (!processes.TryAdd(new ProcessKey(definition.Id.Value, definition.Version), descriptor))
                    throw new WorkflowModuleLoadException("EW7029_DUPLICATE_PROCESS_VERSION",
                        $"Process '{definition.Id.Value}' version {definition.Version} is declared by more than one module.");
            }
        }
        return new WorkflowPresentationCatalog(processes.ToImmutable(), forms.ToImmutable());
    }

    private readonly record struct ProcessKey(string Id, int Version);
    private readonly record struct FormKey(
        string ModuleId, string ModuleVersion, string ArtifactSha256, string FormId, int FormVersion);
}

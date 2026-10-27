using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;

namespace EnterpriseWorkflow.Sdk;

public sealed record WorkflowPermissionDefinition(TechnicalId Id, string Label);

/// <summary>Collects module-owned permission names independently of installation profiles.</summary>
public sealed class WorkflowPermissionRegistryBuilder(WorkflowModuleInfo module)
{
    private readonly Dictionary<string, WorkflowPermissionDefinition> _permissions = new(StringComparer.Ordinal);
    public IReadOnlyCollection<WorkflowPermissionDefinition> Permissions => _permissions.Values;
    public WorkflowPermissionRegistryBuilder Add(string id, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (label.Length > 200) throw new ArgumentOutOfRangeException(nameof(label));
        var qualified = module.Qualify(id);
        if (!_permissions.TryAdd(qualified.Value, new(qualified, label)))
            throw new InvalidOperationException($"Permission '{qualified}' is declared more than once.");
        return this;
    }
}

/// <summary>Collects declarations before resolving references; declaration order has no meaning.</summary>
public sealed class WorkflowModuleBuilder(WorkflowModuleInfo info)
{
    private readonly List<Action<WorkflowHandlerRegistryBuilder>> _handlers = [];
    private readonly Dictionary<Type, WorkflowHandlerReference> _handlerReferences = [];
    private readonly Dictionary<string, WorkflowFormBuilder> _forms = new(StringComparer.Ordinal);
    private readonly List<WorkflowProcessBuilder> _processes = [];
    private readonly List<WorkflowArtifactReference> _dependencies = [];
    private readonly Dictionary<WorkflowHandlerReference, WorkflowArtifactReference> _importedHandlers = [];
    private readonly List<(WorkflowProcessDeclaration Declaration, WorkflowArtifactReference Owner)> _reusedProcesses = [];
    private readonly WorkflowMediaPolicyRegistryBuilder _mediaPolicies = new();
    private readonly WorkflowMediaFormatRegistryBuilder _mediaFormats = new();
    public WorkflowModuleBuilder MediaFormats(Action<WorkflowMediaFormatRegistryBuilder> configure) { configure(_mediaFormats); return this; }
    internal Dictionary<string, WorkflowMediaPolicy> BuildMediaPolicies(IReadOnlySet<string>? known = null) => _mediaPolicies.Build(Info, _mediaFormats.Build(Info), known);
    public WorkflowModuleBuilder MediaPolicies(Action<WorkflowMediaPolicyRegistryBuilder> configure) { configure(_mediaPolicies); return this; }
    public WorkflowModuleInfo Info { get; } = info;

    public WorkflowModuleBuilder AddHumanTask<THandler>(string id) where THandler : class, IHumanTaskCompletionHandler
    {
        var reference = RegisterHandler<THandler>(id);
        _handlers.Add(registry => registry.AddHumanTask<THandler>(reference.Id.Value, reference.Version));
        return this;
    }

    public WorkflowModuleBuilder AddService<THandler>(string id) where THandler : class, IServiceNodeHandler
    {
        var reference = RegisterHandler<THandler>(id);
        _handlers.Add(registry => registry.AddService<THandler>(reference.Id.Value, reference.Version));
        return this;
    }

    /// <summary>References an existing handler without registering a replacement implementation.</summary>
    public WorkflowModuleBuilder UseHandler<THandler>(WorkflowHandlerReference reference, WorkflowArtifactReference owner)
    {
        AddDependency(owner);
        _importedHandlers.Add(reference, owner);
        if (!_handlerReferences.TryAdd(typeof(THandler), reference)) throw new InvalidOperationException("Ambiguous handler type.");
        return this;
    }

    public WorkflowModuleBuilder AddForm(string id, string title, Action<WorkflowFormBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WorkflowFormBuilder(new(Info.Qualify(id), Info.ContractRevision), title, this);
        configure(builder);
        if (!_forms.TryAdd(id, builder)) throw new InvalidOperationException($"Duplicate form '{id}'.");
        return this;
    }

    /// <summary>References a form owned by a retained exact artifact, without redeclaring ownership.</summary>
    public WorkflowModuleBuilder UseForm(string localId, WorkflowFormDefinition form,
        WorkflowHandlerReference handler, WorkflowArtifactReference owner)
    {
        _ = new TechnicalId(localId);
        if (form.Fields.Any(field => field.Kind == WorkflowFormFieldKind.File))
            throw new InvalidOperationException("File forms must be declared with their policies and validators in the process module; use AddForm for shared file forms in that module.");
        AddDependency(owner);
        if (!_forms.TryAdd(localId, WorkflowFormBuilder.Reference(form, handler, owner)))
            throw new InvalidOperationException($"Duplicate form '{localId}'.");
        return this;
    }

    /// <summary>Re-exposes a published process with its original definition and artifact identity.</summary>
    public WorkflowModuleBuilder UseProcess(WorkflowProcessDescriptor process)
    {
        ArgumentNullException.ThrowIfNull(process);
        AddDependency(process.ModuleArtifact);
        var declaration = new WorkflowProcessDeclaration(process.Definition.Id, process.Definition.Version,
            process.Title, process.Description, process.StartForm, process.InitialStateSchemaVersion,
            _ => process.Definition, process.DesignatedAssignmentNodes, process.IconResourcePath, process.StateInitializerType, process.StateValidatorTypes);
        _reusedProcesses.Add((declaration, process.ModuleArtifact));
        return this;
    }

    public WorkflowModuleBuilder AddProcess<TProcess>() where TProcess : IWorkflowProcess, new() => AddProcess(new TProcess().Configure);

    public WorkflowModuleBuilder AddProcess(Action<WorkflowProcessBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WorkflowProcessBuilder(this);
        configure(builder);
        _processes.Add(builder);
        return this;
    }

    public IReadOnlyDictionary<string, Type> Components => _forms.Values.Where(form => !form.IsReference && form.ComponentId is not null)
        .ToDictionary(form => form.ComponentId!.Value.Value, form => form.ComponentType!, StringComparer.Ordinal);

    public void ConfigureHandlers(WorkflowHandlerRegistryBuilder registry)
    {
        foreach (var register in _handlers) register(registry);
        registry.AddHumanTask<StandardHumanTaskHandler>(Info.Qualify("standard.complete").Value, Info.ContractRevision);
    }

    public void ConfigurePresentation(WorkflowPresentationRegistryBuilder registry, IReadOnlyCollection<WorkflowPermissionDefinition> permissions)
    {
        registry.MediaFormats.AddRange(_mediaFormats.Build(Info));
        _ = BuildMediaPolicies(permissions.Select(permission => permission.Id.Value).ToHashSet(StringComparer.Ordinal));
        var known = permissions.Select(permission => permission.Id.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var (reference, owner) in _importedHandlers) registry.ReferenceHandler(reference, owner);
        foreach (var form in _forms.Values)
        {
            var definition = form.Build(ResolveHandler);
            if (form.IsReference) registry.ReferenceForm(definition.Reference, form.Owner);
            else registry.AddForm(definition);
        }
        // The standard form is owned by this artifact, so old UI resources are retained with it.
        registry.AddForm(new(new(Info.Qualify("standard.approval"), Info.ContractRevision), "Décision",
            [new(new("comment"), "Commentaire", WorkflowFormFieldKind.LongText, false,
                WorkflowFieldAudience.TaskParticipant, MaximumLength: 4000)]));
        foreach (var process in _processes) registry.AddProcess(process.Build(known));
        foreach (var (process, owner) in _reusedProcesses) registry.ReferenceProcess(process, owner);
    }

    internal WorkflowFormBuilder Form(string id) => _forms.TryGetValue(id, out var form)
        ? form : throw new InvalidOperationException($"Unknown form '{id}' in module '{Info.Id}'.");
    internal WorkflowHandlerReference ResolveHandler(Type type) => _handlerReferences.TryGetValue(type, out var reference)
        ? reference : throw new InvalidOperationException($"Handler '{type.Name}' must be registered exactly once in module '{Info.Id}'.");
    internal IReadOnlyList<WorkflowArtifactReference> Dependencies => _dependencies;
    private WorkflowHandlerReference RegisterHandler<THandler>(string id)
    {
        var reference = new WorkflowHandlerReference(Info.Qualify(id), Info.ContractRevision);
        if (!_handlerReferences.TryAdd(typeof(THandler), reference))
            throw new InvalidOperationException($"Handler type '{typeof(THandler).Name}' is ambiguous; use an explicit durable reference.");
        return reference;
    }
    private void AddDependency(WorkflowArtifactReference owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_dependencies.Any(item => item.Id == owner.Id && item.Version == owner.Version && item.Sha256 != owner.Sha256))
            throw new InvalidOperationException("Conflicting artifact hashes.");
        if (!_dependencies.Contains(owner)) _dependencies.Add(owner);
    }
}

public interface IWorkflowProcess
{
    void Configure(WorkflowProcessBuilder process);
}

public sealed class WorkflowFormBuilder
{
    private readonly WorkflowModuleBuilder? _module;
    private readonly WorkflowFormReference _reference;
    private readonly string _title;
    private readonly List<WorkflowFieldBuilder> _fields = [];
    private Type? _handlerType;
    private WorkflowHandlerReference? _handler;
    private TechnicalId? _component;
    private WorkflowFormDefinition? _existing;
    internal TechnicalId? ComponentId => _component;
    internal Type? ComponentType { get; private set; }
    private WorkflowFieldAudience _audience = WorkflowFieldAudience.Initiator;
    internal WorkflowFormBuilder(WorkflowFormReference reference, string title, WorkflowModuleBuilder? module = null) { _reference = reference; _title = title; _module = module; }
    internal bool IsReference => _existing is not null;
    internal WorkflowArtifactReference? Owner { get; private set; }
    internal WorkflowFormReference FormReference => _reference;
    public WorkflowFormBuilder HandledBy<THandler>() where THandler : class, IHumanTaskCompletionHandler { _handlerType = typeof(THandler); return this; }
    public WorkflowFormBuilder HandledBy(WorkflowHandlerReference handler) { _handler = handler; return this; }
    public WorkflowFormBuilder ReadableBy(WorkflowFieldAudience audience) { _audience = audience; return this; }
    public WorkflowFormBuilder WithComponent<TComponent>(string? componentId = null)
    {
        _component = new(componentId ?? $"{_reference.Id.Value}.component");
        ComponentType = typeof(TComponent);
        return this;
    }
    public WorkflowFormBuilder Fields(Action<WorkflowFieldsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new WorkflowFieldsBuilder(_fields, _audience));
        return this;
    }
    internal WorkflowHandlerReference Handler(Func<Type, WorkflowHandlerReference> resolve) =>
        _handler ?? (_handlerType is not null ? resolve(_handlerType) : throw new InvalidOperationException($"Form '{_reference.Id}' requires HandledBy."));
    internal WorkflowFormDefinition Build(Func<Type, WorkflowHandlerReference> resolve)
    {
        _ = Handler(resolve);
        return _existing ?? new(_reference, _title, _fields.Select(field => field.Build(_module)).ToImmutableArray(), _component);
    }
    internal static WorkflowFormBuilder Reference(WorkflowFormDefinition form, WorkflowHandlerReference handler, WorkflowArtifactReference owner) =>
        new(form.Reference, form.Title) { _existing = form, _handler = handler, Owner = owner };
}

public sealed class WorkflowFieldsBuilder
{
    private readonly List<WorkflowFieldBuilder> _fields;
    private readonly WorkflowFieldAudience _audience;
    internal WorkflowFieldsBuilder(List<WorkflowFieldBuilder> fields, WorkflowFieldAudience audience) { _fields = fields; _audience = audience; }
    public WorkflowFieldBuilder LongText(string id, string label) => Add(id, label, WorkflowFormFieldKind.LongText);
    public WorkflowFieldBuilder Text(string id, string label) => Add(id, label, WorkflowFormFieldKind.ShortText);
    public WorkflowFieldBuilder Date(string id, string label) => Add(id, label, WorkflowFormFieldKind.Date);
    public WorkflowFieldBuilder Choice(string id, string label) => Add(id, label, WorkflowFormFieldKind.Choice);
    public WorkflowFieldBuilder Number(string id, string label) => Add(id, label, WorkflowFormFieldKind.Number);
    public WorkflowFieldBuilder File(string id, string label) => Add(id, label, WorkflowFormFieldKind.File);
    public WorkflowFieldBuilder Boolean(string id, string label) => Add(id, label, WorkflowFormFieldKind.Boolean);
    private WorkflowFieldBuilder Add(string id, string label, WorkflowFormFieldKind kind)
    {
        var field = new WorkflowFieldBuilder(new(new(id), label, kind, false, _audience));
        _fields.Add(field);
        return field;
    }
}

public sealed class WorkflowFieldBuilder
{
    private WorkflowFormField _field;
    internal WorkflowFieldBuilder(WorkflowFormField field) => _field = field;
    public WorkflowFieldBuilder UseMediaPolicy(string id) { if (_field.Kind != WorkflowFormFieldKind.File) throw new InvalidOperationException("Media policies apply to file fields only."); _ = new TechnicalId(id); _field = _field with { FileSpecification = new(0, _field.FileSpecification?.MaximumFiles ?? 1, [], [], id) }; return this; }
    public WorkflowFieldBuilder MaximumFiles(int count) { if (_field.Kind != WorkflowFormFieldKind.File) throw new InvalidOperationException("File count applies to file fields only."); if (count is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(count)); _field = _field with { FileSpecification = (_field.FileSpecification ?? new(0, 1, [], [])) with { MaximumFiles = count } }; return this; }
    public WorkflowFieldBuilder Required() { _field = _field with { Required = true }; return this; }
    public WorkflowFieldBuilder ReadableBy(WorkflowFieldAudience audience) { _field = _field with { ReadableBy = audience }; return this; }
    public WorkflowFieldBuilder Length(int minimum, int maximum) { _field = _field with { MinimumLength = minimum, MaximumLength = maximum }; return this; }
    public WorkflowFieldBuilder Help(string text) { _field = _field with { HelpText = text }; return this; }
    public WorkflowFieldBuilder Options(params (string Value, string Label)[] choices)
    {
        _field = _field with { Options = choices.Select(choice => new WorkflowChoiceOption(choice.Value, choice.Label)).ToImmutableArray() };
        return this;
    }
    internal WorkflowFormField Build(WorkflowModuleBuilder? module = null)
    {
        if (_field.Kind != WorkflowFormFieldKind.File || module is null) return _field;
        var policies = module.BuildMediaPolicies();
        if (_field.FileSpecification?.PolicyId is not { } id)
        {
            throw new InvalidOperationException("File fields must reference a module media policy.");
        }
        var qualified = module.Info.Qualify(id).Value;
        if (!policies.TryGetValue(qualified, out var policy)) throw new InvalidOperationException($"Unknown module media policy '{id}'.");
        return _field with { FileSpecification = new(policy.MaximumBytes, _field.FileSpecification.MaximumFiles, policy.AllowedContentTypes, policy.AllowedExtensions, qualified, policy) };
    }
}

using System.Reflection;
using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;

namespace EnterpriseWorkflow.Sdk;

/// <summary>Each call adds a required group; every group must match at least one permission.</summary>
public abstract class WorkflowPermissionBuilder<TSelf> where TSelf : WorkflowPermissionBuilder<TSelf>
{
    internal List<string[]> PermissionGroups { get; } = [];
    public TSelf RequiresPermission(string permission)
    {
        _ = new TechnicalId(permission);
        PermissionGroups.Add([permission]);
        return (TSelf)this;
    }
    public TSelf RequiresAnyPermission(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Length == 0) throw new ArgumentException("At least one permission is required.", nameof(permissions));
        foreach (var id in permissions) _ = new TechnicalId(id);
        PermissionGroups.Add(permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        return (TSelf)this;
    }
    internal string[][] ResolvePermissions(WorkflowModuleInfo module, IReadOnlySet<string> known) =>
        ResolveGroups(PermissionGroups, module, known);
    internal static string[][] ResolveGroups(IEnumerable<string[]> groups, WorkflowModuleInfo module, IReadOnlySet<string> known) =>
        groups.Select(group => group.Select(local =>
        {
            var qualified = module.Qualify(local).Value;
            return known.Contains(qualified) ? qualified : throw new InvalidOperationException($"Unknown permission '{local}' in module '{module.Id}'.");
        }).Order(StringComparer.Ordinal).ToArray()).OrderBy(group => string.Join('|', group), StringComparer.Ordinal).ToArray();
}

public sealed class WorkflowProcessBuilder : WorkflowPermissionBuilder<WorkflowProcessBuilder>
{
    private readonly WorkflowModuleBuilder _module;
    private string? _id;
    private string? _name;
    private string? _description;
    private int? _schema;
    private Type? _stateType;
    private Type? _initializerType;
    private string? _startForm;
    private string? _first;
    private readonly List<string[]> _readPermissions = [];
    private readonly List<string[]> _cancelPermissions = [];
    private readonly List<WorkflowStageBuilder> _stages = [];
    internal WorkflowProcessBuilder(WorkflowModuleBuilder module) => _module = module;
    public WorkflowProcessBuilder WithId(string id) { _ = new TechnicalId(id); _id = id; return this; }
    public WorkflowProcessBuilder WithName(string name) { _name = name; return this; }
    public WorkflowProcessBuilder WithDescription(string description) { _description = description; return this; }
    public WorkflowTypedProcessBuilder<TState> WithState<TState>() where TState : class { if (_stateType is not null) throw new InvalidOperationException("State type is already declared."); _stateType = typeof(TState); return new(this); }
    internal WorkflowProcessBuilder InitializedBy<TInitializer>() where TInitializer : class, IWorkflowStateInitializer { _initializerType = typeof(TInitializer); return this; }
    internal WorkflowProcessBuilder WithStateSchemaVersion(int version) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version); _schema = version; return this; }
    internal WorkflowProcessBuilder ReadableByPermission(string permission) { _readPermissions.Add([permission]); return this; }
    internal WorkflowProcessBuilder CancellableByPermission(string permission) { _cancelPermissions.Add([permission]); return this; }
    internal WorkflowProcessBuilder StartWithForm(string formId) { _startForm = formId; return this; }
    internal WorkflowProcessBuilder Then(string stageId) { _first = stageId; return this; }
    internal WorkflowProcessBuilder Approval(string id, string label, Action<WorkflowStageBuilder> configure) => Human(id, label, HumanTaskKind.Approval, configure);
    internal WorkflowProcessBuilder Input(string id, string label, Action<WorkflowStageBuilder> configure) => Human(id, label, HumanTaskKind.Input, configure);
    private WorkflowProcessBuilder Human(string id, string label, HumanTaskKind kind, Action<WorkflowStageBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var stage = new WorkflowStageBuilder(id, label, kind);
        configure(stage);
        _stages.Add(stage);
        return this;
    }
    internal WorkflowStageBuilder Service<THandler>(string id) where THandler : class, IServiceNodeHandler
    {
        var stage = new WorkflowStageBuilder(id, id, null) { ServiceType = typeof(THandler) };
        _stages.Add(stage);
        return stage;
    }
    internal WorkflowProcessBuilder End(string id) { _stages.Add(new WorkflowStageBuilder(id, id, null)); return this; }
    internal WorkflowProcessBuilder End(string id, WorkflowCompletionOutcome outcome, string? label)
    {
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        _stages.Add(new WorkflowStageBuilder(id, label ?? id, null) { CompletionOutcome = outcome }); return this;
    }

    internal WorkflowProcessDeclaration Build(IReadOnlySet<string> known)
    {
        if (_id is null || _schema is null || _startForm is null || _first is null)
            throw new InvalidOperationException("A process requires an explicit ID, state schema version, start form and first stage.");
        if (_stateType is null) throw new InvalidOperationException("Every process requires WithState<TState>().");
        if (_initializerType is null) throw new InvalidOperationException("A typed state requires an explicit start initializer.");
        var startForm = _module.Form(_startForm);
        var permissions = ResolvePermissions(_module.Info, known);
        if (permissions.Length == 0) throw new InvalidOperationException($"Process '{_id}' requires a start permission.");
        var stages = _stages.ToDictionary(stage => stage.Id, StringComparer.Ordinal);
        if (stages.ContainsKey("start")) throw new InvalidOperationException("Stage ID 'start' is reserved for the generated entry node.");
        foreach (var stage in stages.Values)
        {
            stage.Policy = new WorkflowStagePolicy
            {
                CompletionOutcome = stage.CompletionOutcome,
                MediaFields = stage.FormId is null ? null : MediaFields(_module.Form(stage.FormId).Build(_module.ResolveHandler)),
                StateValidationGroups = stage.ValidationGroups.Count == 0 ? null : stage.ValidationGroups.ToArray(),
                Permissions = stage.ResolvePermissions(_module.Info, known),
                AssignedToInitiator = stage.Initiator,
                AssignedFromPreviousTask = stage.Previous,
                SummaryFields = stage.Summary.ToArray(),
                Actions = stage.ActionBuilders.ToDictionary(action => action.Id, action => new WorkflowActionPolicy
                {
                    StateValidationGroups = action.ValidationGroups.Count == 0 ? null : action.ValidationGroups.ToArray(),
                    Label = action.Label,
                    Permissions = action.ResolvePermissions(_module.Info, known),
                    RequireComment = action.CommentRequired,
                    CommentPath = action.CommentPath,
                    CaptureCommentActorName = action.CommentPath is not null,
                    AssigneeTarget = action.AssigneeTarget,
                }, StringComparer.Ordinal),
            };
            if (stage.ActionBuilders.Any(action => action.CommentPath is not null) && (stage.Kind != HumanTaskKind.Approval || stage.HandlerType is not null || stage.FormId is not null))
                throw new InvalidOperationException("Declarative comments require the built-in approval handler and form.");
            if (stage.ActionBuilders.Any(action => action.CommentPath is not null))
            {
                var stateContract = WorkflowStateContractCompiler.Compile(_stateType!, _schema.Value, id => id);
                foreach (var action in stage.ActionBuilders.Where(action => action.CommentPath is not null))
                {
                    var field = stateContract.Root;
                    foreach (var part in action.CommentPath!) field = field.Fields.SingleOrDefault(candidate => candidate.Name == part) ?? throw new InvalidOperationException("Unknown comment state path.");
                    if (field.Kind != "array" || field.Item?.Kind != "object") throw new InvalidOperationException("A comment target must be a collection of WorkflowComment.");
                }
            }
            stage.Policy.Validate();
            if (stage.Kind is not null)
            {
                if (stage.Mode is null) throw new InvalidOperationException($"Stage '{stage.Id}' requires an assignment policy.");
                if (stage.Policy.Permissions.Length == 0 && stage.ActionBuilders.Any(action => stage.Policy.Actions[action.Id].Permissions.Length == 0))
                    throw new InvalidOperationException($"Stage '{stage.Id}' has an action without required permissions.");
                foreach (var field in stage.Summary)
                {
                    var stateFields = WorkflowStateContractCompiler.Compile(_stateType!, _schema.Value, id => id).Root.Fields;
                    if (!stateFields.Any(candidate => candidate.Name == field && !candidate.Hidden))
                        throw new InvalidOperationException($"Stage '{stage.Id}' summary references unreadable or unknown field '{field}'.");
                }
            }
            foreach (var action in stage.ActionBuilders.Where(action => action.AssigneeTarget is not null))
            {
                if (action.Target != action.AssigneeTarget || !stages.TryGetValue(action.AssigneeTarget!, out var target) || !target.Previous)
                    throw new InvalidOperationException($"Stage '{stage.Id}', action '{action.Id}': selection must target the directly reached designated task.");
            }
        }
        foreach (var designated in stages.Values.Where(stage => stage.Previous))
        {
            if (_first == designated.Id || stages.Values.Any(source => source.Next == designated.Id) ||
                stages.Values.SelectMany(source => source.ActionBuilders).Any(action => action.Target == designated.Id && action.AssigneeTarget != designated.Id))
                throw new InvalidOperationException($"Every incoming action to '{designated.Id}' must select its assignee.");
        }
        var placeholder = new WorkflowArtifactReference(_module.Info.Id, _module.Info.ReleaseVersion, new string('0', 64));
        var template = Compile(placeholder, stages, permissions, known);
        return new(_module.Info.Qualify(_id), _module.Info.ContractRevision,
            _name ?? _id, _description ?? _name ?? _id, startForm.FormReference, _schema.Value,
            artifact => WorkflowArtifactBinding.Bind(template, placeholder, artifact), [], StateInitializerType: _initializerType, StateValidatorTypes: _stateType?.GetCustomAttributes<StateValidatorAttribute>().Select(attribute => attribute.ValidatorType).Distinct().ToArray());
    }

    private static Dictionary<string, string>? MediaFields(WorkflowFormDefinition form)
    {
        var fields = form.Fields.Where(field => field.FileSpecification?.PolicyId is not null).ToDictionary(field => field.Id.Value, field => field.FileSpecification!.PolicyId!, StringComparer.Ordinal);
        return fields.Count == 0 ? null : fields;
    }

    private WorkflowDefinition Compile(WorkflowArtifactReference artifact, Dictionary<string, WorkflowStageBuilder> stages,
        string[][] permissions, IReadOnlySet<string> known)
    {
        var startPolicy = new WorkflowStagePolicy
        {
            MediaPolicyModuleVersion = _module.BuildMediaPolicies(known).Count > 0 ? _module.Info.ReleaseVersion : null,
            MediaPolicyModule = _module.BuildMediaPolicies(known).Count > 0 ? _module.Info.Id.Value : null,
            MediaPolicies = _module.BuildMediaPolicies(known) is { Count: > 0 } mediaPolicies ? mediaPolicies : null,
            MediaFields = MediaFields(_module.Form(_startForm!).Build(_module.ResolveHandler)),
            StateContract = WorkflowStateContractCompiler.Compile(_stateType!, _schema!.Value, id =>
            {
                var qualified = _module.Info.Qualify(id).Value;
                return known.Contains(qualified) ? qualified : throw new InvalidOperationException($"Unknown state field permission '{id}'.");
            }),
            Permissions = permissions,
            ReadPermissions = ResolveGroups(_readPermissions, _module.Info, known),
            CancelPermissions = ResolveGroups(_cancelPermissions, _module.Info, known),
        };
        var builder = WorkflowBuilder.Create(_module.Info, _id!).WithDisplayLabel(_name)
            .RequiresArtifact(artifact.Id.Value, artifact.Version, artifact.Sha256)
            .Start("start", startPolicy.ToConfigurationJson()).Then("start", _first!);
        foreach (var dependency in _module.Dependencies)
            builder.RequiresArtifact(dependency.Id.Value, dependency.Version, dependency.Sha256);
        foreach (var stage in stages.Values)
        {
            var configuration = stage.Policy!.ToConfigurationJson();
            if (stage.Kind is { } kind)
            {
                var form = stage.FormId is not null ? _module.Form(stage.FormId) : null;
                var formReference = form?.FormReference ?? new WorkflowFormReference(_module.Info.Qualify("standard.approval"), _module.Info.ContractRevision);
                var handler = stage.HandlerType is not null ? _module.ResolveHandler(stage.HandlerType) :
                    form?.Handler(_module.ResolveHandler) ?? new WorkflowHandlerReference(_module.Info.Qualify("standard.complete"), _module.Info.ContractRevision);
                builder.HumanTask(stage.Id, kind, stage.Mode!.Value, formReference.Id.Value, formReference.Version,
                    handler.Id.Value, stage.ActionBuilders.Select(action => new HumanTaskActionDraft(action.Id, action.Id)),
                    handler.Version, allowInitiator: stage.InitiatorAllowed ?? kind is not HumanTaskKind.Approval,
                    distinctFrom: stage.ActorConstraints, configurationJson: configuration, displayLabel: stage.Label);
                foreach (var action in stage.ActionBuilders) builder.On(stage.Id, action.Id, action.Target ?? throw new InvalidOperationException($"Missing destination for '{stage.Id}/{action.Id}'."));
            }
            else if (stage.ServiceType is { } service)
            {
                var handler = _module.ResolveHandler(service);
                builder.Service(stage.Id, handler.Id.Value, handler.Version, configuration, stage.Label)
                    .Then(stage.Id, stage.Next ?? throw new InvalidOperationException($"Service '{stage.Id}' requires a destination."));
            }
            else builder.End(stage.Id, configuration, stage.Label);
        }
        var compiled = builder.Validate();
        if (!compiled.IsPublishable || compiled.Definition is null)
            throw new InvalidOperationException($"Process '{_id}': " + string.Join("; ", compiled.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        return compiled.Definition;
    }
}

public sealed class WorkflowStageBuilder : WorkflowPermissionBuilder<WorkflowStageBuilder>
{
    internal WorkflowStageBuilder(string id, string label, HumanTaskKind? kind) { _ = new TechnicalId(id); Id = id; Label = label; Kind = kind; }
    internal string Id { get; }
    internal string Label { get; }
    internal HumanTaskKind? Kind { get; }
    internal WorkflowCompletionOutcome? CompletionOutcome { get; init; }
    internal HumanTaskAssignmentMode? Mode { get; private set; }
    internal bool Initiator { get; private set; }
    internal bool Previous { get; private set; }
    internal bool? InitiatorAllowed { get; private set; }
    internal List<HumanTaskActorConstraintDraft> ActorConstraints { get; } = [];
    internal Type? ServiceType { get; init; }
    internal Type? HandlerType { get; private set; }
    internal string? FormId { get; private set; }
    internal string? Next { get; private set; }
    internal List<string> Summary { get; } = [];
    internal List<string> ValidationGroups { get; } = [];
    public WorkflowStageBuilder ValidateState(params string[] groups) { ValidationGroups.AddRange(groups); return this; }
    internal List<WorkflowActionBuilder> ActionBuilders { get; } = [];
    internal WorkflowStagePolicy? Policy { get; set; }
    public WorkflowStageBuilder ClaimedByEligibleParticipant() { SetMode(HumanTaskAssignmentMode.EligiblePool, false, false); return this; }
    public WorkflowStageBuilder AssignedFromPreviousTask() { SetMode(HumanTaskAssignmentMode.DesignatedIdentity, false, true); return this; }
    public WorkflowStageBuilder AssignedToInitiator() { SetMode(HumanTaskAssignmentMode.DesignatedIdentity, true, false); return this; }
    private void SetMode(HumanTaskAssignmentMode mode, bool initiator, bool previous)
    {
        if (Mode is not null) throw new InvalidOperationException($"Assignment policy already declared for '{Id}'.");
        if (initiator && Kind == HumanTaskKind.Approval) throw new InvalidOperationException("Initiator confirmation must be an input task.");
        Mode = mode; Initiator = initiator; Previous = previous;
    }
    public WorkflowStageBuilder AllowInitiator(bool allow = true) { InitiatorAllowed = allow; return this; }
    public WorkflowStageBuilder DistinctFrom(string stageId, string actionId)
    {
        ActorConstraints.Add(new(stageId, actionId));
        return this;
    }
    public WorkflowStageBuilder UsesForm(string id) { FormId = id; return this; }
    public WorkflowStageBuilder HandledBy<THandler>() where THandler : class, IHumanTaskCompletionHandler { if (Kind == HumanTaskKind.Approval) throw new InvalidOperationException("Approval nodes use the built-in handler."); HandlerType = typeof(THandler); return this; }
    public WorkflowStageBuilder ShowFields(params string[] fields) { Summary.AddRange(fields); return this; }
    public WorkflowStageBuilder Actions(Action<WorkflowActionsBuilder> configure) { configure(new(ActionBuilders)); return this; }
    public WorkflowStageBuilder Then(string id) { Next = id; return this; }
}

public sealed class WorkflowActionsBuilder
{
    private readonly List<WorkflowActionBuilder> _actions;
    internal WorkflowActionsBuilder(List<WorkflowActionBuilder> actions) => _actions = actions;
    public WorkflowActionsBuilder Add(string id, string label, Action<WorkflowActionBuilder> configure)
    {
        var action = new WorkflowActionBuilder(id, label);
        configure(action);
        _actions.Add(action);
        return this;
    }
}

public sealed class WorkflowActionBuilder : WorkflowPermissionBuilder<WorkflowActionBuilder>
{
    internal List<string> ValidationGroups { get; } = [];
    public WorkflowActionBuilder ValidateState(params string[] groups) { ValidationGroups.AddRange(groups); return this; }

    internal WorkflowActionBuilder(string id, string label) { _ = new TechnicalId(id); Id = id; Label = label; }
    internal string Id { get; }
    internal string Label { get; }
    internal string? Target { get; private set; }
    internal string? AssigneeTarget { get; private set; }
    internal bool CommentRequired { get; private set; }
    public WorkflowActionBuilder Then(string id) { Target = id; return this; }
    internal string[]? CommentPath { get; private set; }
    internal void SetComment(string[] path, bool required) { if (CommentPath is not null) throw new InvalidOperationException("Comment target is already declared."); CommentPath = path; CommentRequired = required; }
    public WorkflowActionBuilder SelectAssigneeFor(string id) { AssigneeTarget = id; return this; }
}

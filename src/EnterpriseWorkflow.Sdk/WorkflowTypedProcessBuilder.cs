using EnterpriseWorkflow.Core.Model;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Runtime;

namespace EnterpriseWorkflow.Sdk;

/// <summary>Propagates the process DTO to stage and action expressions.</summary>
public sealed class WorkflowTypedProcessBuilder<TState> where TState : class
{
    private readonly WorkflowProcessBuilder _inner;
    internal WorkflowTypedProcessBuilder(WorkflowProcessBuilder inner) => _inner = inner;
    public WorkflowTypedProcessBuilder<TState> WithId(string id) { _inner.WithId(id); return this; }
    public WorkflowTypedProcessBuilder<TState> WithName(string name) { _inner.WithName(name); return this; }
    public WorkflowTypedProcessBuilder<TState> WithDescription(string description) { _inner.WithDescription(description); return this; }
    public WorkflowTypedProcessBuilder<TState> InitializedBy<T>() where T : class, IWorkflowStateInitializer { _inner.InitializedBy<T>(); return this; }
    public WorkflowTypedProcessBuilder<TState> WithStateSchemaVersion(int version) { _inner.WithStateSchemaVersion(version); return this; }
    public WorkflowTypedProcessBuilder<TState> RequiresPermission(string permission) { _inner.RequiresPermission(permission); return this; }
    public WorkflowTypedProcessBuilder<TState> RequiresAnyPermission(params string[] permissions) { _inner.RequiresAnyPermission(permissions); return this; }
    public WorkflowTypedProcessBuilder<TState> ReadableByPermission(string permission) { _inner.ReadableByPermission(permission); return this; }
    public WorkflowTypedProcessBuilder<TState> CancellableByPermission(string permission) { _inner.CancellableByPermission(permission); return this; }
    public WorkflowTypedProcessBuilder<TState> StartWithForm(string id) { _inner.StartWithForm(id); return this; }
    public WorkflowTypedProcessBuilder<TState> Then(string id) { _inner.Then(id); return this; }
    public WorkflowTypedProcessBuilder<TState> End(string id) { _inner.End(id); return this; }
    public WorkflowTypedProcessBuilder<TState> End(string id, WorkflowCompletionOutcome outcome, string? label = null) { _inner.End(id, outcome, label); return this; }
    public WorkflowStageBuilder Service<T>(string id) where T : class, IServiceNodeHandler => _inner.Service<T>(id);
    public WorkflowTypedProcessBuilder<TState> Approval(string id, string label, Action<WorkflowTypedStageBuilder<TState>> configure)
    { _inner.Approval(id, label, stage => configure(new(stage))); return this; }
    public WorkflowTypedProcessBuilder<TState> Input(string id, string label, Action<WorkflowTypedStageBuilder<TState>> configure)
    { _inner.Input(id, label, stage => configure(new(stage))); return this; }
}

public sealed class WorkflowTypedStageBuilder<TState> where TState : class
{
    private readonly WorkflowStageBuilder _inner;
    internal WorkflowTypedStageBuilder(WorkflowStageBuilder inner) => _inner = inner;
    public WorkflowTypedStageBuilder<TState> RequiresPermission(string value) { _inner.RequiresPermission(value); return this; }
    public WorkflowTypedStageBuilder<TState> RequiresAnyPermission(params string[] values) { _inner.RequiresAnyPermission(values); return this; }
    public WorkflowTypedStageBuilder<TState> ValidateState(params string[] groups) { _inner.ValidateState(groups); return this; }
    public WorkflowTypedStageBuilder<TState> ClaimedByEligibleParticipant() { _inner.ClaimedByEligibleParticipant(); return this; }
    public WorkflowTypedStageBuilder<TState> AssignedToInitiator() { _inner.AssignedToInitiator(); return this; }
    public WorkflowTypedStageBuilder<TState> AssignedFromPreviousTask() { _inner.AssignedFromPreviousTask(); return this; }
    public WorkflowTypedStageBuilder<TState> AllowInitiator(bool allow = true) { _inner.AllowInitiator(allow); return this; }
    public WorkflowTypedStageBuilder<TState> DistinctFrom(string stage, string action) { _inner.DistinctFrom(stage, action); return this; }
    public WorkflowTypedStageBuilder<TState> UsesForm(string id) { _inner.UsesForm(id); return this; }
    public WorkflowTypedStageBuilder<TState> HandledBy<T>() where T : class, IHumanTaskCompletionHandler { _inner.HandledBy<T>(); return this; }
    public WorkflowTypedStageBuilder<TState> ShowFields(params string[] fields) { _inner.ShowFields(fields); return this; }
    public WorkflowTypedStageBuilder<TState> Then(string id) { _inner.Then(id); return this; }
    public WorkflowTypedStageBuilder<TState> Actions(Action<WorkflowTypedActionsBuilder<TState>> configure)
    { _inner.Actions(actions => configure(new(actions))); return this; }
}

public sealed class WorkflowTypedActionsBuilder<TState> where TState : class
{
    private readonly WorkflowActionsBuilder _inner;
    internal WorkflowTypedActionsBuilder(WorkflowActionsBuilder inner) => _inner = inner;
    public WorkflowTypedActionsBuilder<TState> Add(string id, string label, Action<WorkflowTypedActionBuilder<TState>> configure)
    { _inner.Add(id, label, action => configure(new(action))); return this; }
}

public sealed class WorkflowTypedActionBuilder<TState> where TState : class
{
    private readonly WorkflowActionBuilder _inner;
    internal WorkflowTypedActionBuilder(WorkflowActionBuilder inner) => _inner = inner;
    public WorkflowTypedActionBuilder<TState> Then(string id) { _inner.Then(id); return this; }
    public WorkflowTypedActionBuilder<TState> SelectAssigneeFor(string id) { _inner.SelectAssigneeFor(id); return this; }
    public WorkflowTypedActionBuilder<TState> RequiresPermission(string value) { _inner.RequiresPermission(value); return this; }
    public WorkflowTypedActionBuilder<TState> RequiresAnyPermission(params string[] values) { _inner.RequiresAnyPermission(values); return this; }
    public WorkflowTypedActionBuilder<TState> ValidateState(params string[] groups) { _inner.ValidateState(groups); return this; }
    public WorkflowTypedActionBuilder<TState> RequireCommentAt(Expression<Func<TState, ICollection<WorkflowComment>>> target) => SetComment(target, true);
    public WorkflowTypedActionBuilder<TState> CommentAt(Expression<Func<TState, ICollection<WorkflowComment>>> target) => SetComment(target, false);
    private WorkflowTypedActionBuilder<TState> SetComment(Expression<Func<TState, ICollection<WorkflowComment>>> target, bool required)
    {
        ArgumentNullException.ThrowIfNull(target);
        Expression current = target.Body;
        if (current is UnaryExpression { NodeType: ExpressionType.Convert } convert) current = convert.Operand;
        var path = new List<string>();
        while (current is MemberExpression { Member: PropertyInfo property, Expression: { } parent })
        {
            if (property.GetIndexParameters().Length != 0 || property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                throw new InvalidOperationException("A comment target must be a serialized property path.");
            path.Add(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name);
            current = parent;
        }
        if (current != target.Parameters[0] || path.Count == 0) throw new InvalidOperationException("A comment target must be a property of the process state.");
        path.Reverse();
        _inner.SetComment(path.ToArray(), required);
        return this;
    }
}

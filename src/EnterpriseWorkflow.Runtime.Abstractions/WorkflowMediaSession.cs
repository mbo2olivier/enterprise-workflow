using System.Text.Json;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

/// <summary>One execution's file operations. The runtime seals and commits the collected changes.</summary>
public sealed class WorkflowMediaSession
{
    private readonly WorkflowMediaWriteScope _scope;
    private readonly Dictionary<string, WorkflowMediaPolicy> _policies;
    private readonly Dictionary<string, string> _fields;
    private readonly CanonicalJson? _submission;
    private readonly string? _module;
    private readonly HashSet<WorkflowMediaReference> _attachments = [];
    private readonly HashSet<WorkflowMediaReference> _deletions = [];
    private readonly object _gate = new();
    private IWorkflowMediaService? _service;
    private bool _sealed;
    private WorkflowMediaSession(WorkflowMediaWriteScope scope, Dictionary<string, WorkflowMediaPolicy> policies,
        Dictionary<string, string> fields, CanonicalJson? submission, string? module)
    { _scope = scope; _policies = policies; _fields = fields; _submission = submission; _module = module; }

    public static WorkflowMediaSession Create(WorkflowDefinition definition, CanonicalJson nodeConfiguration,
        WorkflowMediaWriteScope scope, CanonicalJson? submission = null)
    {
        var start = definition.Nodes.Single(node => node.Role == WorkflowNodeRole.Start);
        var module = WorkflowStagePolicy.Read(start.Configuration)?.MediaPolicyModule;
        var moduleVersion = WorkflowStagePolicy.Read(start.Configuration)?.MediaPolicyModuleVersion;
        var artifact = module is null ? null : definition.Artifacts.Single(item => item.Id.Value == module && item.Version == moduleVersion);
        return new(scope with { Artifact = artifact }, WorkflowStagePolicy.Read(start.Configuration)?.MediaPolicies ?? [],
            WorkflowStagePolicy.Read(nodeConfiguration)?.MediaFields ?? [], submission, WorkflowStagePolicy.Read(start.Configuration)?.MediaPolicyModule);
    }
    public WorkflowMediaSession Bind(IWorkflowMediaService service)
    {
        lock (_gate) { EnsureOpen(); if (_service is not null && !ReferenceEquals(_service, service)) throw new InvalidOperationException("Media context is already bound."); _service = service; return this; }
    }
    private IWorkflowMediaService Service { get { lock (_gate) { EnsureOpen(); return _service ?? throw new InvalidOperationException("Unbound media context."); } } }
    private void EnsureOpen() { if (_sealed) throw new InvalidOperationException("Media execution has ended."); }
    public (WorkflowMediaReference[] Attachments, WorkflowMediaReference[] Deletions) Seal()
    { lock (_gate) { EnsureOpen(); _sealed = true; return (_attachments.ToArray(), _deletions.ToArray()); } }
    private WorkflowMediaPolicy Policy(string id)
    {
        if (_policies.TryGetValue(id, out var exact)) return exact;
        if (_module is not null) return _policies.TryGetValue(_module + "." + id, out var local) ? local : throw new InvalidOperationException($"Unknown module media policy '{id}'.");
        var matches = _policies.Values.Where(policy => policy.Id.EndsWith("." + id, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException($"Unknown or ambiguous module media policy '{id}'.");
    }
    public ValueTask<WorkflowMediaRecord> StoreAsync(string key, string name, Stream content, string policy, CancellationToken cancellationToken = default) =>
        Store(key, name, content, Policy(policy), null, cancellationToken);
    private async ValueTask<WorkflowMediaRecord> Store(string key, string name, Stream content, WorkflowMediaPolicy policy,
        WorkflowMediaReference? replace, CancellationToken token)
    {
        var result = await Service.StageAsync(_scope, key, name, content,
            new(policy.MaximumBytes, policy.AllowedContentTypes, policy.AllowedExtensions, policy.Formats), new(policy.Hidden, policy.PermissionGroups), token, replace);
        if (result.Status == WorkflowMediaStatus.Quarantined) throw new WorkflowMediaInspectionPendingException();
        lock (_gate) { EnsureOpen(); if (_deletions.Any(item => item.Id == result.Reference.Id)) throw new InvalidOperationException("Conflicting file operations."); _attachments.Add(result.Reference); }
        return result;
    }
    public async ValueTask<WorkflowMediaRecord> ReplaceAsync(WorkflowMediaReference reference, string key, string name, Stream content, string policy, CancellationToken cancellationToken = default)
    {
        if (await GetAsync(reference, cancellationToken) is not { Status: WorkflowMediaStatus.Active }) throw new FileNotFoundException("Media unavailable.");
        return await Store(key, name, content, Policy(policy), reference, cancellationToken);
    }
    public ValueTask<WorkflowMediaRecord?> GetAsync(WorkflowMediaReference reference, CancellationToken cancellationToken = default) => Service.GetForInstanceAsync(Instance(), reference, cancellationToken);
    public ValueTask<Stream> OpenAsync(WorkflowMediaReference reference, CancellationToken cancellationToken = default) => Service.OpenForInstanceAsync(Instance(), reference, cancellationToken);
    public async ValueTask DeleteAsync(WorkflowMediaReference reference, CancellationToken cancellationToken = default)
    {
        if (await GetAsync(reference, cancellationToken) is not { Status: WorkflowMediaStatus.Active }) throw new FileNotFoundException("Media unavailable.");
        lock (_gate) { EnsureOpen(); if (_attachments.Any(item => item.Id == reference.Id)) throw new InvalidOperationException("Conflicting file operations."); _deletions.Add(reference); }
    }
    private WorkflowInstanceId Instance() => _scope.InstanceId ?? throw new InvalidOperationException("The instance has not been created yet.");
    public async ValueTask<IReadOnlyList<WorkflowMediaRecord>> FromFieldAsync(string field, CancellationToken cancellationToken = default)
    {
        if (!_fields.TryGetValue(field, out var policyId) || _submission is null) throw new InvalidOperationException($"Unknown file field '{field}'.");
        var policy = Policy(policyId);
        var references = _submission.ToJsonElement().TryGetProperty(field, out var value) ? value.Deserialize<WorkflowMediaReference[]>() ?? [] : [];
        if (references.Length > 8 || references.Distinct().Count() != references.Length) throw new InvalidDataException("Invalid file references.");
        var result = new List<WorkflowMediaRecord>();
        foreach (var reference in references)
        {
            var item = await Service.GetStagedAsync(reference, cancellationToken);
            if (item is null || item.Status != WorkflowMediaStatus.Staged || item.Owner != _scope.Owner || item.InstanceId != _scope.InstanceId ||
                item.ActivationId != _scope.ActivationId || item.SubmissionKey != _scope.SubmissionKey || item.SourceField != field ||
                item.Length > policy.MaximumBytes || !policy.AllowedContentTypes.Contains(item.ContentType, StringComparer.OrdinalIgnoreCase) ||
                !policy.AllowedExtensions.Contains(Path.GetExtension(item.Name), StringComparer.OrdinalIgnoreCase) || item.Hidden != policy.Hidden ||
                JsonSerializer.Serialize(item.PermissionGroups) != JsonSerializer.Serialize(policy.PermissionGroups)) throw new InvalidDataException("File does not belong to this field and submission.");
            result.Add(item);
        }
        lock (_gate) { EnsureOpen(); foreach (var item in result) _attachments.Add(item.Reference); }
        return result;
    }
}

public sealed class WorkflowMediaInspectionPendingException : InvalidOperationException
{
    public WorkflowMediaInspectionPendingException() : base("Media inspection is pending; retry the operation.") { }
}

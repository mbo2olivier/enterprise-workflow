using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Sdk;

public enum MediaFormats { Pdf, Jpeg, Png, Text, Csv, Docx, Xlsx, Pptx, Odt, Ods, Odp, Rtf, Tiff }

public sealed class WorkflowMediaPolicyRegistryBuilder
{
    private readonly Dictionary<string, WorkflowMediaPolicyBuilder> _policies = new(StringComparer.Ordinal);
    public WorkflowMediaPolicyBuilder Add(string id)
    {
        _ = new EnterpriseWorkflow.Abstractions.TechnicalId(id);
        var policy = new WorkflowMediaPolicyBuilder(id);
        if (!_policies.TryAdd(id, policy)) throw new InvalidOperationException($"Duplicate media policy '{id}'.");
        return policy;
    }
    internal Dictionary<string, WorkflowMediaPolicy> Build(WorkflowModuleInfo module, IReadOnlyList<EnterpriseWorkflow.Presentation.WorkflowMediaFormatDeclaration> formats, IReadOnlySet<string>? known = null) =>
        _policies.Values.Select(value => value.Build(module, formats, known)).ToDictionary(value => value.Id, StringComparer.Ordinal);
}

public sealed class WorkflowMediaPolicyBuilder
{
    private readonly string _id;
    private string _label;
    private long _bytes;
    private string[] _formats = [];
    private bool? _hidden;
    private readonly List<string[]> _permissions = [];
    internal WorkflowMediaPolicyBuilder(string id) { _id = id; _label = id; }
    public WorkflowMediaPolicyBuilder Label(string label) { _label = label; return this; }
    public WorkflowMediaPolicyBuilder AllowFormats(params MediaFormats[] formats) { _formats = formats.Select(value => value.ToString().ToLowerInvariant()).ToArray(); return this; }
    public WorkflowMediaPolicyBuilder AllowFormats(params string[] formats) { _formats = formats; return this; }
    public WorkflowMediaPolicyBuilder MaximumSize(long bytes) { _bytes = bytes; return this; }
    public WorkflowMediaPolicyBuilder VisibleToInstanceReaders() { SetVisibility(false); return this; }
    public WorkflowMediaPolicyBuilder Hidden() { SetVisibility(true); return this; }
    public WorkflowMediaPolicyBuilder VisibleFor(params string[] permissions)
    {
        if (permissions.Length == 0 || _hidden == true) throw new InvalidOperationException("Invalid media visibility.");
        _hidden = false; _permissions.Add(permissions); return this;
    }
    private void SetVisibility(bool hidden)
    {
        if (_hidden is not null) throw new InvalidOperationException("Media visibility is already declared.");
        _hidden = hidden;
    }
    internal WorkflowMediaPolicy Build(WorkflowModuleInfo module, IReadOnlyList<EnterpriseWorkflow.Presentation.WorkflowMediaFormatDeclaration> customFormats, IReadOnlySet<string>? known)
    {
        if (_hidden is null) throw new InvalidOperationException($"Media policy '{_id}' requires explicit visibility.");
        var formats = _formats.Select(id => WorkflowMediaFormats.Find(id) ?? customFormats.SingleOrDefault(value => value.Definition.Id == module.Qualify(id).Value)?.Definition
            ?? throw new InvalidOperationException($"Unknown module media format '{id}'.")).DistinctBy(format => format.Id).ToArray();
        if (formats.SelectMany(format => format.Extensions).GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Ambiguous file extensions in media policy.");
        var permissions = _permissions.Select(group => group.Select(id => module.Qualify(id).Value).ToArray()).ToArray();
        if (known is not null && permissions.SelectMany(group => group).Any(id => !known.Contains(id))) throw new InvalidOperationException("Undeclared media permission.");
        var result = new WorkflowMediaPolicy(module.Qualify(_id).Value, _label, _bytes, formats.Select(value => value.ContentType).Distinct().ToArray(),
            formats.SelectMany(value => value.Extensions).Distinct().ToArray(), _hidden.Value, permissions, formats);
        result.Validate(); return result;
    }
}

public static class MediaSizeExtensions
{
    public static long Megabytes(this int value) => checked(value * 1024L * 1024L);
}

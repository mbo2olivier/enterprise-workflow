using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;

namespace EnterpriseWorkflow.Sdk;

public sealed class WorkflowMediaFormatRegistryBuilder
{
    private readonly Dictionary<string, WorkflowMediaFormatBuilder> _formats = new(StringComparer.Ordinal);
    public WorkflowMediaFormatBuilder Add<TValidator>(string id) where TValidator : class, IWorkflowMediaFormatValidator
    {
        _ = new EnterpriseWorkflow.Abstractions.TechnicalId(id);
        if (WorkflowMediaFormats.Find(id) is not null) throw new InvalidOperationException("Built-in formats cannot be overridden.");
        var builder = new WorkflowMediaFormatBuilder(id, typeof(TValidator));
        if (!_formats.TryAdd(id, builder)) throw new InvalidOperationException("Duplicate media format.");
        return builder;
    }
    internal IReadOnlyList<WorkflowMediaFormatDeclaration> Build(WorkflowModuleInfo module) => _formats.Values.Select(value => value.Build(module)).ToArray();
}

public sealed class WorkflowMediaFormatBuilder
{
    private readonly string _id;
    private readonly Type _validator;
    private string? _mime;
    private string[] _extensions = [];
    internal WorkflowMediaFormatBuilder(string id, Type validator) { _id = id; _validator = validator; }
    public WorkflowMediaFormatBuilder ContentType(string mime) { _mime = mime; return this; }
    public WorkflowMediaFormatBuilder Extensions(params string[] extensions) { _extensions = extensions.Select(value => value.ToLowerInvariant()).ToArray(); return this; }
    internal WorkflowMediaFormatDeclaration Build(WorkflowModuleInfo module)
    {
        if (_validator.IsAbstract) throw new InvalidOperationException("A format validator must be concrete.");
        var format = new WorkflowMediaFormatDefinition(module.Qualify(_id).Value, _mime ?? "", _extensions);
        format.Validate();
        if (format.Extensions.Any(extension => WorkflowMediaFormats.BuiltIns.Any(builtIn => builtIn.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Built-in file extensions cannot be overridden by a custom format.");
        return new(format, _validator);
    }
}

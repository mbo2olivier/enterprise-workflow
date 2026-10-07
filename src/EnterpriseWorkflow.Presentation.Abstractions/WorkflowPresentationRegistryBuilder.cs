using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Presentation;

/// <summary>Collects one trusted module's immutable presentation declarations at startup.</summary>
public sealed class WorkflowPresentationRegistryBuilder
{
    private readonly Dictionary<(string Id, int Version), WorkflowFormDefinition> _forms = [];
    private readonly Dictionary<(string Id, int Version), WorkflowProcessDeclaration> _processes = [];

    public IReadOnlyCollection<WorkflowFormDefinition> Forms => _forms.Values;
    public IReadOnlyCollection<WorkflowProcessDeclaration> Processes => _processes.Values;

    public WorkflowPresentationRegistryBuilder AddForm(WorkflowFormDefinition form)
    {
        ArgumentNullException.ThrowIfNull(form);
        ValidateForm(form);
        if (!_forms.TryAdd((form.Reference.Id.Value, form.Reference.Version), form))
            throw new InvalidOperationException($"Form '{form.Reference.Id.Value}' version {form.Reference.Version} is declared more than once by the module.");
        return this;
    }

    public WorkflowPresentationRegistryBuilder AddProcess(WorkflowProcessDeclaration process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (process.Version <= 0) throw new ArgumentOutOfRangeException(nameof(process), "A process version must be positive.");
        RequiredText(process.Title, 200, "Process title");
        RequiredText(process.Description, 1000, "Process description");
        if (process.InitialStateSchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(process), "An initial state schema version must be positive.");
        ArgumentNullException.ThrowIfNull(process.DefinitionFactory);
        if (!_forms.ContainsKey((process.StartForm.Id.Value, process.StartForm.Version)))
            throw new InvalidOperationException($"Start form '{process.StartForm.Id.Value}' version {process.StartForm.Version} must be declared before process '{process.Id.Value}'.");
        var assignments = process.DesignatedAssignmentNodes.IsDefault
            ? []
            : process.DesignatedAssignmentNodes;
        if (assignments.Distinct().Count() != assignments.Length)
            throw new InvalidOperationException($"Process '{process.Id.Value}' declares a designated assignment node more than once.");
        if (!_processes.TryAdd((process.Id.Value, process.Version), process with { DesignatedAssignmentNodes = assignments }))
            throw new InvalidOperationException($"Process '{process.Id.Value}' version {process.Version} is declared more than once by the module.");
        return this;
    }

    private static void ValidateForm(WorkflowFormDefinition form)
    {
        if (form.Reference.Version <= 0) throw new ArgumentOutOfRangeException(nameof(form), "A form version must be positive.");
        RequiredText(form.Title, 200, "Form title");
        if (form.Description is not null && form.Description.Length > 1000)
            throw new ArgumentException("Form description is limited to 1000 characters.", nameof(form));
        if (form.Fields.IsDefaultOrEmpty || form.Fields.Length > 128)
            throw new ArgumentException("A form must declare between 1 and 128 fields.", nameof(form));
        if (form.Fields.Select(field => field.Id).Distinct().Count() != form.Fields.Length)
            throw new ArgumentException("Form field identifiers must be unique.", nameof(form));

        foreach (var field in form.Fields)
        {
            RequiredText(field.Label, 200, $"Field '{field.Id.Value}' label");
            if (field.ReadableBy is WorkflowFieldAudience.None ||
                (field.ReadableBy & ~(WorkflowFieldAudience.Initiator | WorkflowFieldAudience.TaskParticipant |
                    WorkflowFieldAudience.InstanceReader)) != 0)
                throw new ArgumentException($"Field '{field.Id.Value}' must declare supported audiences.", nameof(form));
            if (field.MinimumLength is < 0 || field.MaximumLength is < 1 ||
                field.MinimumLength > field.MaximumLength)
                throw new ArgumentException($"Field '{field.Id.Value}' has invalid text length bounds.", nameof(form));
            if (field.MaximumLength > 32_768)
                throw new ArgumentException($"Field '{field.Id.Value}' exceeds the maximum text length.", nameof(form));
            if (field.Minimum > field.Maximum)
                throw new ArgumentException($"Field '{field.Id.Value}' has invalid numeric bounds.", nameof(form));
            if (field.Pattern is { Length: > 512 })
                throw new ArgumentException($"Field '{field.Id.Value}' pattern is too long.", nameof(form));
            var options = field.Options.IsDefault ? [] : field.Options;
            if (field.Kind is WorkflowFormFieldKind.Choice)
            {
                if (options.Length is 0 or > 100 || options.Select(option => option.Value).Distinct(StringComparer.Ordinal).Count() != options.Length)
                    throw new ArgumentException($"Choice field '{field.Id.Value}' must declare 1 to 100 unique options.", nameof(form));
                foreach (var option in options)
                {
                    RequiredText(option.Value, 256, $"Choice '{field.Id.Value}' value");
                    RequiredText(option.Label, 200, $"Choice '{field.Id.Value}' label");
                }
            }
            else if (!options.IsEmpty)
            {
                throw new ArgumentException($"Field '{field.Id.Value}' declares choices but is not a choice field.", nameof(form));
            }
        }
    }

    private static void RequiredText(string value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw new ArgumentException($"{field} is required and limited to {maximumLength} characters.");
    }
}

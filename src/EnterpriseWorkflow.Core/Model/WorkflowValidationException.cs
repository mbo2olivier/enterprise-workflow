namespace EnterpriseWorkflow.Core.Model;

/// <summary>Explicit user-facing rejection from a state initializer. Unexpected exceptions are never displayed.</summary>
public sealed class WorkflowValidationException : Exception
{
    public WorkflowValidationException(params WorkflowValidationError[] errors) : base("Workflow validation failed.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Length is < 1 or > 50 || errors.Any(error => error is null)) throw new ArgumentException("Provide 1 to 50 errors.", nameof(errors));
        Errors = Array.AsReadOnly(errors.ToArray());
    }
    public IReadOnlyList<WorkflowValidationError> Errors { get; }
}

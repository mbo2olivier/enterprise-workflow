namespace EnterpriseWorkflow.Core.Diagnostics;

/// <summary>
/// Severity of a workflow definition diagnostic.
/// </summary>
public enum WorkflowDiagnosticSeverity
{
    /// <summary>An informational observation.</summary>
    Information,

    /// <summary>A non-blocking issue.</summary>
    Warning,

    /// <summary>An issue that prevents publication.</summary>
    Error,
}

/// <summary>
/// A stable, localized diagnostic emitted while compiling a workflow draft.
/// </summary>
public sealed record WorkflowDiagnostic(
    string Code,
    WorkflowDiagnosticSeverity Severity,
    string Location,
    string Message);


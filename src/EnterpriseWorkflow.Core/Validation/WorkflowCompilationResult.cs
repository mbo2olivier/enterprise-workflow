using System.Collections.Immutable;
using EnterpriseWorkflow.Core.Diagnostics;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Core.Validation;

/// <summary>
/// Result of explicit validation and canonical compilation.
/// </summary>
public sealed class WorkflowCompilationResult
{
    internal WorkflowCompilationResult(WorkflowDefinition? definition, IEnumerable<WorkflowDiagnostic> diagnostics)
    {
        Definition = definition;
        Diagnostics = diagnostics
            .OrderBy(item => item.Location, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Gets the canonical definition only when no error was emitted.</summary>
    public WorkflowDefinition? Definition { get; }

    /// <summary>Gets stable diagnostics ordered by location and code.</summary>
    public ImmutableArray<WorkflowDiagnostic> Diagnostics { get; }

    /// <summary>Gets a value indicating whether this result can be published.</summary>
    public bool IsPublishable => Definition is not null &&
        Diagnostics.All(item => item.Severity is not WorkflowDiagnosticSeverity.Error);
}


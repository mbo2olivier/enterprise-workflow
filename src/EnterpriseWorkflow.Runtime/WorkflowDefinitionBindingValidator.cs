using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>One deterministic definition-to-registry binding failure.</summary>
public sealed record WorkflowBindingDiagnostic(
    string Code,
    TechnicalId NodeId,
    WorkflowHandlerReference Handler,
    string Message);

/// <summary>Result of validating every executable node against an immutable registry.</summary>
public sealed class WorkflowBindingValidationResult
{
    internal WorkflowBindingValidationResult(ImmutableArray<WorkflowBindingDiagnostic> diagnostics) =>
        Diagnostics = diagnostics;

    /// <summary>Gets a value indicating whether publication may proceed.</summary>
    public bool IsValid => Diagnostics.IsEmpty;

    /// <summary>Gets deterministic binding diagnostics.</summary>
    public ImmutableArray<WorkflowBindingDiagnostic> Diagnostics { get; }

    /// <summary>Throws when a definition cannot be bound before publication.</summary>
    public void EnsureValid()
    {
        if (!IsValid)
        {
            throw new WorkflowBindingValidationException(Diagnostics);
        }
    }
}

/// <summary>Validates global handler keys and executable roles before publication.</summary>
public static class WorkflowDefinitionBindingValidator
{
    /// <summary>Validates a canonical definition against the exact immutable runtime registry.</summary>
    public static WorkflowBindingValidationResult Validate(
        WorkflowDefinition definition,
        IWorkflowHandlerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registry);
        var diagnostics = ImmutableArray.CreateBuilder<WorkflowBindingDiagnostic>();

        foreach (var node in definition.Nodes.Where(item => item.Handler is not null))
        {
            var handler = node.Handler!;
            if (!registry.TryGet(handler, out var descriptor))
            {
                diagnostics.Add(new WorkflowBindingDiagnostic(
                    "EW4002_HANDLER_NOT_FOUND",
                    node.Id,
                    handler,
                    $"Handler '{handler.Id.Value}' version {handler.Version} is not registered."));
                continue;
            }

            var expectedKind = node.Role switch
            {
                WorkflowNodeRole.Service => WorkflowHandlerKind.Service,
                WorkflowNodeRole.Decision => WorkflowHandlerKind.Decision,
                _ => throw new InvalidOperationException($"Node '{node.Id.Value}' unexpectedly declares a handler."),
            };
            if (descriptor!.Kind != expectedKind)
            {
                diagnostics.Add(new WorkflowBindingDiagnostic(
                    "EW4003_HANDLER_KIND_MISMATCH",
                    node.Id,
                    handler,
                    $"Handler '{handler.Id.Value}' version {handler.Version} is registered as {descriptor.Kind} but node '{node.Id.Value}' requires {expectedKind}."));
            }
        }

        return new WorkflowBindingValidationResult(diagnostics.ToImmutable());
    }
}

/// <summary>Prevents publication of a canonical definition with unresolved executable bindings.</summary>
public sealed class WorkflowBindingValidationException : InvalidOperationException
{
    /// <summary>Creates a failure containing all deterministic diagnostics.</summary>
    public WorkflowBindingValidationException(ImmutableArray<WorkflowBindingDiagnostic> diagnostics)
        : base(CreateMessage(diagnostics)) => Diagnostics = diagnostics;

    /// <summary>Gets every binding diagnostic.</summary>
    public ImmutableArray<WorkflowBindingDiagnostic> Diagnostics { get; }

    private static string CreateMessage(ImmutableArray<WorkflowBindingDiagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty)
        {
            throw new ArgumentException("At least one binding diagnostic is required.", nameof(diagnostics));
        }

        return string.Join(Environment.NewLine, diagnostics.Select(item => $"{item.Code}: {item.Message}"));
    }
}

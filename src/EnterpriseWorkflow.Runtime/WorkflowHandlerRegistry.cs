using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Executable role implemented by one globally versioned handler.</summary>
public enum WorkflowHandlerKind
{
    /// <summary>Handler executes a service node.</summary>
    Service,

    /// <summary>Handler evaluates an exclusive decision.</summary>
    Decision,
}

/// <summary>Immutable binding between a durable key and one concrete handler type.</summary>
public sealed record WorkflowHandlerDescriptor(
    WorkflowHandlerReference Reference,
    WorkflowHandlerKind Kind,
    Type HandlerType);

/// <summary>Read-only registry used by validation and scoped resolution.</summary>
public interface IWorkflowHandlerRegistry
{
    /// <summary>Gets all handlers ordered by ordinal identifier and version.</summary>
    ImmutableArray<WorkflowHandlerDescriptor> Handlers { get; }

    /// <summary>Finds an exact global identifier/version binding.</summary>
    bool TryGet(WorkflowHandlerReference reference, out WorkflowHandlerDescriptor? descriptor);
}

/// <summary>Collects direct application registrations before creating an immutable registry.</summary>
public sealed class WorkflowHandlerRegistryBuilder
{
    private readonly Dictionary<WorkflowHandlerReference, WorkflowHandlerDescriptor> _descriptors = [];

    /// <summary>Registers a scoped service-node implementation.</summary>
    public WorkflowHandlerRegistryBuilder AddService<THandler>(string id, int version)
        where THandler : class, IServiceNodeHandler =>
        Add<THandler>(id, version, WorkflowHandlerKind.Service);

    /// <summary>Registers a scoped decision-node implementation.</summary>
    public WorkflowHandlerRegistryBuilder AddDecision<THandler>(string id, int version)
        where THandler : class, IDecisionNodeHandler =>
        Add<THandler>(id, version, WorkflowHandlerKind.Decision);

    /// <summary>Builds a detached immutable registry.</summary>
    public IWorkflowHandlerRegistry Build() => new WorkflowHandlerRegistry(_descriptors.Values);

    internal ImmutableArray<WorkflowHandlerDescriptor> BuildDescriptors() => Build().Handlers;

    private WorkflowHandlerRegistryBuilder Add<THandler>(string id, int version, WorkflowHandlerKind kind)
        where THandler : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        var reference = new WorkflowHandlerReference(new TechnicalId(id), version);
        var descriptor = new WorkflowHandlerDescriptor(reference, kind, typeof(THandler));
        if (!_descriptors.TryAdd(reference, descriptor))
        {
            throw new WorkflowHandlerRegistrationException(
                "EW4001_DUPLICATE_HANDLER",
                $"Handler '{id}' version {version} is registered more than once.");
        }

        return this;
    }
}

/// <summary>Reports an invalid or ambiguous registry declaration during startup.</summary>
public sealed class WorkflowHandlerRegistrationException : InvalidOperationException
{
    /// <summary>Creates a startup registration failure.</summary>
    public WorkflowHandlerRegistrationException(string code, string message)
        : base($"{code}: {message}") => Code = code;

    /// <summary>Gets the stable diagnostic code.</summary>
    public string Code { get; }
}

internal sealed class WorkflowHandlerRegistry : IWorkflowHandlerRegistry
{
    private readonly ImmutableDictionary<WorkflowHandlerReference, WorkflowHandlerDescriptor> _byReference;

    internal WorkflowHandlerRegistry(IEnumerable<WorkflowHandlerDescriptor> descriptors)
    {
        Handlers = descriptors
            .OrderBy(item => item.Reference.Id.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Reference.Version)
            .ToImmutableArray();
        _byReference = Handlers.ToImmutableDictionary(item => item.Reference);
    }

    public ImmutableArray<WorkflowHandlerDescriptor> Handlers { get; }

    public bool TryGet(WorkflowHandlerReference reference, out WorkflowHandlerDescriptor? descriptor) =>
        _byReference.TryGetValue(reference, out descriptor);
}

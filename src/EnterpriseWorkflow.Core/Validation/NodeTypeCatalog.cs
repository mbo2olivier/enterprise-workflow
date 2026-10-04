using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Diagnostics;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Core.Validation;

/// <summary>
/// Validates a node configuration without loading or executing a module.
/// </summary>
public interface INodeConfigurationValidator
{
    /// <summary>Returns configuration diagnostics localized below the supplied node location.</summary>
    IEnumerable<WorkflowDiagnostic> Validate(CanonicalJson configuration, string nodeLocation);
}

/// <summary>
/// Describes a node type known by an application-provided catalog.
/// </summary>
public sealed record NodeTypeDescriptor(
    NodeTypeReference Type,
    WorkflowNodeRole Role,
    INodeConfigurationValidator? ConfigurationValidator = null);

/// <summary>
/// Immutable catalog used for reference and configuration validation.
/// </summary>
public sealed class NodeTypeCatalog
{
    private readonly ImmutableDictionary<NodeTypeReference, NodeTypeDescriptor> _descriptors;

    /// <summary>Initializes a catalog from unique type/version pairs.</summary>
    public NodeTypeCatalog(IEnumerable<NodeTypeDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _descriptors = descriptors.ToImmutableDictionary(item => item.Type);
    }

    /// <summary>Gets the catalog containing the built-in workflow node types.</summary>
    public static NodeTypeCatalog BuiltIns { get; } = new(
    [
        new(new NodeTypeReference(new TechnicalId("core.start"), 1), WorkflowNodeRole.Start),
        new(new NodeTypeReference(new TechnicalId("core.service"), 1), WorkflowNodeRole.Service),
        new(new NodeTypeReference(new TechnicalId("core.decision"), 1), WorkflowNodeRole.Decision),
        new(new NodeTypeReference(new TechnicalId("core.human-task"), 1), WorkflowNodeRole.HumanTask),
        new(new NodeTypeReference(new TechnicalId("core.timer"), 1), WorkflowNodeRole.Timer),
        new(new NodeTypeReference(new TechnicalId("core.end"), 1), WorkflowNodeRole.End),
    ]);

    /// <summary>Attempts to resolve an exact node type contract version.</summary>
    public bool TryGet(NodeTypeReference type, out NodeTypeDescriptor? descriptor) =>
        _descriptors.TryGetValue(type, out descriptor);

    /// <summary>Returns a new catalog with one added or replaced exact type/version descriptor.</summary>
    public NodeTypeCatalog With(NodeTypeDescriptor descriptor) =>
        new(_descriptors.Values.Where(item => item.Type != descriptor.Type).Append(descriptor));
}

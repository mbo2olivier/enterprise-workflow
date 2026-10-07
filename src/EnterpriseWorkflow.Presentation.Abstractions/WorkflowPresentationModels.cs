using System.Collections.Immutable;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Presentation;

public enum WorkflowFormFieldKind
{
    ShortText,
    LongText,
    Date,
    Number,
    Boolean,
    Choice,
}

[Flags]
public enum WorkflowFieldAudience
{
    None = 0,
    Initiator = 1,
    TaskParticipant = 2,
    InstanceReader = 4,
}

public sealed record WorkflowChoiceOption(string Value, string Label);

public sealed record WorkflowFormField(
    TechnicalId Id,
    string Label,
    WorkflowFormFieldKind Kind,
    bool Required,
    WorkflowFieldAudience ReadableBy,
    int? MinimumLength = null,
    int? MaximumLength = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    string? Pattern = null,
    string? HelpText = null,
    ImmutableArray<WorkflowChoiceOption> Options = default);

public sealed record WorkflowFormDefinition(
    WorkflowFormReference Reference,
    string Title,
    ImmutableArray<WorkflowFormField> Fields,
    TechnicalId? CustomComponentId = null,
    string? Description = null);

/// <summary>
/// Trusted factory invoked only after the loader knows the module's exact immutable artifact identity.
/// The returned definition must reference that artifact.
/// </summary>
public delegate WorkflowDefinition WorkflowDefinitionFactory(WorkflowArtifactReference moduleArtifact);

public sealed record WorkflowProcessDeclaration(
    TechnicalId Id,
    int Version,
    string Title,
    string Description,
    WorkflowFormReference StartForm,
    int InitialStateSchemaVersion,
    WorkflowDefinitionFactory DefinitionFactory,
    ImmutableArray<TechnicalId> DesignatedAssignmentNodes = default,
    string? IconResourcePath = null);

public sealed record WorkflowProcessDescriptor(
    WorkflowDefinition Definition,
    WorkflowArtifactReference ModuleArtifact,
    string Title,
    string Description,
    WorkflowFormReference StartForm,
    int InitialStateSchemaVersion,
    ImmutableArray<TechnicalId> DesignatedAssignmentNodes,
    string? IconResourcePath);

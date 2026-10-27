using System.Reflection;
using EnterpriseWorkflow.Sdk;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Extensions;

/// <summary>
/// Pure state transformation for one declared schema edge. Implementations must not perform external effects.
/// A thrown exception leaves durable state unchanged and the operation can be retried explicitly.
/// </summary>
public interface IWorkflowStateMigrator
{
    ValueTask<WorkflowState> MigrateAsync(WorkflowState source, CancellationToken cancellationToken);
}

/// <summary>Module-local declaration of one exact source-to-target state migration.</summary>
public sealed record WorkflowStateMigrationDeclaration(int SourceSchemaVersion, int TargetSchemaVersion, Type MigratorType, WorkflowStateContract? TargetContract = null, Type[]? TargetValidatorTypes = null, string[]? ValidationGroups = null);

/// <summary>Collects immutable state-migration declarations before the service provider is built.</summary>
public sealed class WorkflowStateMigrationRegistryBuilder
{
    private readonly Dictionary<(int Source, int Target), WorkflowStateMigrationDeclaration> _declarations = [];

    public IReadOnlyCollection<WorkflowStateMigrationDeclaration> Declarations => _declarations.Values;

    public WorkflowStateMigrationRegistryBuilder Add<TMigrator, TTargetState>(int sourceSchemaVersion, int targetSchemaVersion, params string[] validationGroups)
        where TMigrator : IWorkflowStateMigrator
    {
        Add<TMigrator>(sourceSchemaVersion, targetSchemaVersion);
        var key = (sourceSchemaVersion, targetSchemaVersion);
        _declarations[key] = _declarations[key] with
        {
            TargetContract = WorkflowStateContractCompiler.Compile<TTargetState>(targetSchemaVersion),
            TargetValidatorTypes = typeof(TTargetState).GetCustomAttributes<StateValidatorAttribute>().Select(attribute => attribute.ValidatorType).Distinct().ToArray(),
            ValidationGroups = validationGroups,
        };
        return this;
    }

    public WorkflowStateMigrationRegistryBuilder Add<TMigrator>(int sourceSchemaVersion, int targetSchemaVersion)
        where TMigrator : IWorkflowStateMigrator
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceSchemaVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSchemaVersion);
        if (sourceSchemaVersion == targetSchemaVersion)
            throw new ArgumentException("A state migration must change the schema version.");
        var declaration = new WorkflowStateMigrationDeclaration(sourceSchemaVersion, targetSchemaVersion, typeof(TMigrator));
        if (!_declarations.TryAdd((sourceSchemaVersion, targetSchemaVersion), declaration))
            throw new InvalidOperationException($"State migration {sourceSchemaVersion}->{targetSchemaVersion} is declared more than once by the module.");
        return this;
    }
}

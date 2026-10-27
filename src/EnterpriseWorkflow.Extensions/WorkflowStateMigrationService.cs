using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Extensions;

/// <summary>Runs exactly one explicit module-owned state migration and commits it atomically.</summary>
public sealed class WorkflowStateMigrationService(
    IWorkflowMaintenanceStore store,
    IWorkflowStateMigrationRegistry registry,
    IServiceScopeFactory scopeFactory, EnterpriseWorkflow.Core.Serialization.IWorkflowSerializer? serializer = null)
{
    public async ValueTask<StoreResult<StateMigrationResult>> MigrateAsync(
        WorkflowInstanceId instanceId,
        ModuleArtifactReference artifact,
        int targetSchemaVersion,
        ActorIdentity actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSchemaVersion);
        var snapshotResult = await store.ReadStateMigrationSnapshotAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (snapshotResult.Outcome is not StoreOutcome.Succeeded || snapshotResult.Value is not { } snapshot)
            return new StoreResult<StateMigrationResult>(snapshotResult.Outcome, null, snapshotResult.ErrorCode);
        if (!snapshot.Artifacts.Contains(artifact))
            return StoreResults.Conflict<StateMigrationResult>("EW3055_MIGRATOR_ARTIFACT_NOT_REFERENCED");
        if (!registry.TryGet(artifact, snapshot.State.SchemaVersion, targetSchemaVersion, out var registration) || registration is null)
            return StoreResults.NotFound<StateMigrationResult>("EW3057_STATE_MIGRATION_NOT_REGISTERED");

        await using var scope = scopeFactory.CreateAsyncScope();
        var migrator = (IWorkflowStateMigrator)ActivatorUtilities.GetServiceOrCreateInstance(
            scope.ServiceProvider, registration.MigratorType);
        var replacement = await migrator.MigrateAsync(snapshot.State, cancellationToken).ConfigureAwait(false)
            ?? throw new WorkflowStateMigrationException("EW7020_STATE_MIGRATOR_RETURNED_NULL",
                "The state migrator returned null.");
        if (!(serializer ?? new EnterpriseWorkflow.Core.Serialization.WorkflowSerializer()).IsValidState(replacement.Value))
            return StoreResults.Conflict<StateMigrationResult>("runtime.state-invalid");
        if (replacement.SchemaVersion != targetSchemaVersion)
            throw new WorkflowStateMigrationException("EW7021_STATE_MIGRATOR_TARGET_MISMATCH",
                $"The state migrator returned schema {replacement.SchemaVersion}; schema {targetSchemaVersion} was declared.");

        if (registration.TargetContract is { } targetContract)
        {
            var groups = registration.ValidationGroups ?? [];
            if (targetContract.Validate(replacement.Value, groups).Count > 0)
                return StoreResults.Conflict<StateMigrationResult>("runtime.state-invalid");
            foreach (var binding in targetContract.Validators ?? [])
            {
                if (binding.Groups.Length > 0 && !binding.Groups.Any(groups.Contains)) continue;
                var validatorType = registration.TargetValidatorTypes?.SingleOrDefault(type => type.FullName == binding.Id);
                if (validatorType is null) return StoreResults.Conflict<StateMigrationResult>("runtime.state-validator-missing");
                var validator = (IWorkflowStateValidator)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, validatorType);
                if ((await validator.ValidateAsync(new(replacement, null, null, groups), cancellationToken)).Count > 0)
                    return StoreResults.Conflict<StateMigrationResult>("runtime.state-invalid");
            }
        }
        else if (store is IWorkflowStore reads)
        {
            var original = await reads.ReadInstanceAsync(instanceId, cancellationToken);
            if (original.Value is { } current && WorkflowStateContract.From(current.Definition) is not null)
                return StoreResults.Conflict<StateMigrationResult>("runtime.target-state-contract-required");
        }
        return await store.CommitStateMigrationAsync(new CommitStateMigrationCommand(
            instanceId, snapshot.Revision, snapshot.State.SchemaVersion, replacement, artifact, actor),
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class WorkflowStateMigrationException : InvalidOperationException
{
    public WorkflowStateMigrationException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException) => Code = code;

    public string Code { get; }
}

using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Serialization;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Sqlite;

public sealed partial class SqliteWorkflowStore : IWorkflowMaintenanceStore
{
    public async ValueTask<StoreResult<ModuleArtifactReconciliationResult>> ReconcileModuleArtifactsAsync(
        ReconcileModuleArtifactsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.ConfiguredArtifacts);
        if (!TryIndexArtifacts(command.ConfiguredArtifacts, out var configured))
            return StoreResults.Conflict<ModuleArtifactReconciliationResult>("EW3050_INVALID_MODULE_ARTIFACT_SET");

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var requiredArtifacts = await ReadRequiredArtifactsAsync(context, cancellationToken).ConfigureAwait(false);
            if (requiredArtifacts.GroupBy(item => new ModuleArtifactKey(item.Id.Value, item.Version))
                .Any(group => group.Select(item => item.Sha256).Distinct(StringComparer.Ordinal).Skip(1).Any()))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<ModuleArtifactReconciliationResult>("EW3052_MODULE_VERSION_CONTENT_CONFLICT");
            }
            var required = requiredArtifacts.GroupBy(item => new ModuleArtifactKey(item.Id.Value, item.Version))
                .ToDictionary(group => group.Key, group => group.First());
            if (required.Any(item => !configured.TryGetValue(item.Key, out var candidate) ||
                !string.Equals(candidate.Sha256, item.Value.Sha256, StringComparison.Ordinal)))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<ModuleArtifactReconciliationResult>("EW3051_REQUIRED_MODULE_ARTIFACT_MISSING");
            }

            var existing = await context.ModuleArtifacts.ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var row in existing)
            {
                var key = new ModuleArtifactKey(row.ModuleId, row.Version);
                if (configured.TryGetValue(key, out var candidate))
                {
                    if (!string.Equals(row.Sha256, candidate.Sha256, StringComparison.Ordinal))
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return StoreResults.Conflict<ModuleArtifactReconciliationResult>("EW3052_MODULE_VERSION_CONTENT_CONFLICT");
                    }
                }
                else
                {
                    context.ModuleArtifacts.Remove(row);
                }
            }

            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var existingKeys = existing.Select(row => new ModuleArtifactKey(row.ModuleId, row.Version)).ToHashSet();
            foreach (var (key, artifact) in configured)
            {
                if (!existingKeys.Contains(key))
                    context.ModuleArtifacts.Add(new ModuleArtifactRow
                    {
                        ModuleId = artifact.Id.Value,
                        Version = artifact.Version,
                        Sha256 = artifact.Sha256,
                        InstalledAtUnixMilliseconds = now,
                    });
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new ModuleArtifactReconciliationResult(
                configured.Values.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                    .ThenBy(item => item.Version, StringComparer.Ordinal).ToArray()));
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<StoreResult<StateMigrationSnapshot>> ReadStateMigrationSnapshotAsync(
        WorkflowInstanceId instanceId,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var instance = await context.Instances.AsNoTracking().SingleOrDefaultAsync(
                row => row.Id == FormatGuid(instanceId.Value), cancellationToken).ConfigureAwait(false);
            if (instance is null)
                return StoreResults.NotFound<StateMigrationSnapshot>("EW3053_INSTANCE_NOT_FOUND");
            var definitionRow = await context.Definitions.AsNoTracking().SingleAsync(
                row => row.DefinitionId == instance.DefinitionId && row.Version == instance.DefinitionVersion,
                cancellationToken).ConfigureAwait(false);
            var definition = PublishedWorkflowDefinitionReader.Read(definitionRow.CanonicalJson,
                definitionRow.DefinitionId, definitionRow.Version, definitionRow.Sha256);
            return StoreResults.Succeeded(ToMigrationSnapshot(instance, definition));
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<StateMigrationResult>> CommitStateMigrationAsync(
        CommitStateMigrationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var key = FormatGuid(command.InstanceId.Value);
            var instance = await context.Instances.FindAsync([key], cancellationToken).ConfigureAwait(false);
            if (instance is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<StateMigrationResult>("EW3053_INSTANCE_NOT_FOUND");
            }
            if (instance.Revision != command.ExpectedRevision ||
                instance.StateSchemaVersion != command.SourceSchemaVersion ||
                command.ReplacementState.SchemaVersion == command.SourceSchemaVersion)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<StateMigrationResult>("EW3054_STALE_STATE_MIGRATION");
            }

            var definitionRow = await context.Definitions.SingleAsync(
                row => row.DefinitionId == instance.DefinitionId && row.Version == instance.DefinitionVersion,
                cancellationToken).ConfigureAwait(false);
            var definition = PublishedWorkflowDefinitionReader.Read(definitionRow.CanonicalJson,
                definitionRow.DefinitionId, definitionRow.Version, definitionRow.Sha256);
            var installedArtifact = await context.ModuleArtifacts.FindAsync(
                [command.MigratorArtifact.Id.Value, command.MigratorArtifact.Version], cancellationToken).ConfigureAwait(false);
            if (installedArtifact is null ||
                !string.Equals(installedArtifact.Sha256, command.MigratorArtifact.Sha256, StringComparison.Ordinal) ||
                !References(definition, command.MigratorArtifact))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<StateMigrationResult>("EW3055_MIGRATOR_ARTIFACT_NOT_REFERENCED");
            }

            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var hasActiveLease = await context.WorkItems.AnyAsync(row => row.InstanceId == key &&
                row.Status == (int)WorkItemStatus.Leased && row.LeaseExpiresAtUnixMilliseconds > now,
                cancellationToken).ConfigureAwait(false);
            if (hasActiveLease)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<StateMigrationResult>("EW3056_STATE_MIGRATION_INSTANCE_ACTIVE");
            }

            instance.StateJson = command.ReplacementState.Value.CanonicalText;
            instance.StateSchemaVersion = command.ReplacementState.SchemaVersion;
            instance.Revision++;
            instance.UpdatedAtUnixMilliseconds = now;
            context.Audits.Add(new AuditRow
            {
                InstanceId = key,
                EventType = "StateMigrated",
                Revision = instance.Revision,
                OccurredAtUnixMilliseconds = now,
                ActorProviderId = command.Actor.ProviderId.Value,
                ActorSubjectId = command.Actor.SubjectId,
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new StateMigrationResult(instance.Revision, instance.StateSchemaVersion));
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ModuleArtifactReference[]> ReadRequiredArtifactsAsync(
        SqliteWorkflowDbContext context, CancellationToken cancellationToken)
    {
        var definitions = await context.Definitions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return definitions.SelectMany(row => PublishedWorkflowDefinitionReader.Read(
                row.CanonicalJson, row.DefinitionId, row.Version, row.Sha256).Artifacts)
            .Select(item => new ModuleArtifactReference(item.Id, item.Version, item.Sha256))
            .ToArray();
    }

    private static StateMigrationSnapshot ToMigrationSnapshot(InstanceRow instance, WorkflowDefinition definition) => new(
        new WorkflowInstanceId(ParseGuid(instance.Id)),
        new DefinitionReference(definition.Id, definition.Version, definition.Sha256),
        definition.Artifacts.Select(item => new ModuleArtifactReference(item.Id, item.Version, item.Sha256)).ToArray(),
        (WorkflowInstanceStatus)instance.Status,
        instance.Revision,
        WorkflowState.Create(instance.StateJson, instance.StateSchemaVersion));

    private static bool References(WorkflowDefinition definition, ModuleArtifactReference artifact) =>
        definition.Artifacts.Any(item => item.Id == artifact.Id &&
            string.Equals(item.Version, artifact.Version, StringComparison.Ordinal) &&
            string.Equals(item.Sha256, artifact.Sha256, StringComparison.Ordinal));

    private static bool TryIndexArtifacts(IReadOnlyList<ModuleArtifactReference> artifacts,
        out Dictionary<ModuleArtifactKey, ModuleArtifactReference> indexed)
    {
        indexed = new();
        foreach (var artifact in artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.Version) || artifact.Version.Length > 128 ||
                artifact.Sha256.Length != 64 || artifact.Sha256.Any(character =>
                    character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
                !indexed.TryAdd(new ModuleArtifactKey(artifact.Id.Value, artifact.Version), artifact)) return false;
        }
        return true;
    }

    private readonly record struct ModuleArtifactKey(string Id, string Version);
}

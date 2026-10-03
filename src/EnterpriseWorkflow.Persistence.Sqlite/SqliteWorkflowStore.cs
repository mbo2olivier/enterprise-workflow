using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EnterpriseWorkflow.Persistence.Sqlite;

/// <summary>Atomic workflow store backed by a real SQLite database.</summary>
public sealed class SqliteWorkflowStore : IWorkflowStore
{
    private readonly SqliteWorkflowDatabase _database;

    /// <summary>Creates a store. Migrations must be applied explicitly before use.</summary>
    public SqliteWorkflowStore(SqliteWorkflowDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<DefinitionReference>> PublishDefinitionAsync(
        PublishDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var definition = command.Definition;
            var existing = await context.Definitions.FindAsync(
                [definition.Id.Value, definition.Version],
                cancellationToken).ConfigureAwait(false);
            var reference = new DefinitionReference(definition.Id, definition.Version, definition.Sha256);

            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return string.Equals(existing.Sha256, definition.Sha256, StringComparison.Ordinal)
                    ? StoreResults.Idempotent(reference)
                    : StoreResults.Conflict<DefinitionReference>("EW3001_DEFINITION_CONTENT_CONFLICT");
            }

            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            context.Definitions.Add(new DefinitionRow
            {
                DefinitionId = definition.Id.Value,
                Version = definition.Version,
                Sha256 = definition.Sha256,
                SchemaVersion = WorkflowDefinition.SchemaVersion,
                CanonicalJson = definition.CanonicalJson,
                PublishedAtUnixMilliseconds = now,
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(reference);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<StartInstanceResult>> StartInstanceAsync(
        StartInstanceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsUtcMillisecond(command.RequestedAtUtc))
        {
            return StoreResults.Conflict<StartInstanceResult>("EW3002_INVALID_REQUEST_TIME");
        }

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var receipt = await FindReceiptAsync(context, command, cancellationToken).ConfigureAwait(false);
            if (receipt is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                if (!ReceiptMatches(receipt, command))
                {
                    return StoreResults.Conflict<StartInstanceResult>("EW3016_RECEIPT_KEY_COLLISION");
                }

                return string.Equals(receipt.RequestSha256, command.RequestSha256, StringComparison.Ordinal)
                    ? StoreResults.Idempotent(new StartInstanceResult(
                        new WorkflowInstanceId(ParseGuid(receipt.InstanceId)),
                        WasCreated: false,
                        receipt.InstanceRevision))
                    : StoreResults.Conflict<StartInstanceResult>("EW3003_IDEMPOTENCY_KEY_CONFLICT");
            }

            var definition = await context.Definitions.FindAsync(
                [command.Definition.DefinitionId.Value, command.Definition.Version],
                cancellationToken).ConfigureAwait(false);
            if (definition is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<StartInstanceResult>("EW3004_DEFINITION_NOT_FOUND");
            }

            if (!string.Equals(definition.Sha256, command.Definition.Sha256, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<StartInstanceResult>("EW3005_DEFINITION_HASH_MISMATCH");
            }

            var startNodeId = ReadStartNodeId(definition.CanonicalJson);
            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var instanceId = Guid.NewGuid();
            var activationId = Guid.NewGuid();
            var workItemId = Guid.NewGuid();
            var instanceKey = FormatGuid(instanceId);

            context.Instances.Add(new InstanceRow
            {
                Id = instanceKey,
                DefinitionId = definition.DefinitionId,
                DefinitionVersion = definition.Version,
                DefinitionSha256 = definition.Sha256,
                Status = (int)WorkflowInstanceStatus.Created,
                Revision = 0,
                StateSchemaVersion = command.InitialState.SchemaVersion,
                StateJson = command.InitialState.Value.CanonicalText,
                BusinessKey = StoreContractRules.NormalizeOptionalText(command.BusinessKey),
                CorrelationId = StoreContractRules.NormalizeOptionalText(command.CorrelationId),
                CreatedAtUnixMilliseconds = now,
                UpdatedAtUnixMilliseconds = now,
            });
            context.Activations.Add(new ActivationRow
            {
                Id = FormatGuid(activationId),
                InstanceId = instanceKey,
                NodeId = startNodeId,
                Status = (int)NodeExecutionStatus.Pending,
                Attempt = 0,
            });
            context.WorkItems.Add(new WorkItemRow
            {
                Id = FormatGuid(workItemId),
                ActivationId = FormatGuid(activationId),
                InstanceId = instanceKey,
                NodeId = startNodeId,
                Status = (int)WorkItemStatus.Ready,
                DueAtUnixMilliseconds = now,
                Generation = 0,
                CreatedAtUnixMilliseconds = now,
            });
            context.StartReceipts.Add(new StartReceiptRow
            {
                ReceiptKey = StoreContractRules.ComputeStartReceiptKey(command.Scope, command.IdempotencyKey),
                InstallationId = command.Scope.InstallationId.Value,
                CommandTypeId = command.Scope.CommandTypeId.Value,
                ActorProviderId = command.Scope.Actor.ProviderId.Value,
                ActorSubjectId = command.Scope.Actor.SubjectId,
                IdempotencyKey = command.IdempotencyKey.Value,
                RequestSha256 = command.RequestSha256,
                InstanceId = instanceKey,
                InstanceRevision = 0,
                CommittedAtUnixMilliseconds = now,
            });
            AddAudit(context, instanceKey, "InstanceStarted", 0, now, command.Scope.Actor);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new StartInstanceResult(new WorkflowInstanceId(instanceId), true, 0));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<ClaimedWork>> ClaimDueWorkAsync(
        ClaimDueWorkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsWholePositiveMilliseconds(command.LeaseDuration))
        {
            return StoreResults.Conflict<ClaimedWork>("EW3006_INVALID_LEASE_DURATION");
        }

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var item = await context.WorkItems
                .Where(row =>
                    (row.Status == (int)WorkItemStatus.Ready && row.DueAtUnixMilliseconds <= now) ||
                    (row.Status == (int)WorkItemStatus.Leased && row.LeaseExpiresAtUnixMilliseconds <= now))
                .Where(row => context.Instances.Any(instance =>
                    instance.Id == row.InstanceId &&
                    (instance.Status == (int)WorkflowInstanceStatus.Created ||
                     instance.Status == (int)WorkflowInstanceStatus.Running ||
                     instance.Status == (int)WorkflowInstanceStatus.Waiting)))
                .OrderBy(row => row.DueAtUnixMilliseconds)
                .ThenBy(row => row.CreatedAtUnixMilliseconds)
                .ThenBy(row => row.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (item is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<ClaimedWork>("EW3007_NO_DUE_WORK");
            }

            var instance = await context.Instances.FindAsync([item.InstanceId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The claimed work item has no instance.");
            var activation = await context.Activations.FindAsync([item.ActivationId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The claimed work item has no activation.");
            var definitionRow = await context.Definitions.FindAsync(
                [instance.DefinitionId, instance.DefinitionVersion], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The claimed instance has no published definition.");
            var token = Guid.NewGuid();
            var expiresAt = checked(now + (long)command.LeaseDuration.TotalMilliseconds);

            item.Status = (int)WorkItemStatus.Leased;
            item.OwnerId = command.OwnerId.Value;
            item.Generation = checked(item.Generation + 1);
            item.LeaseToken = FormatGuid(token);
            item.LeaseExpiresAtUnixMilliseconds = expiresAt;
            activation.Status = (int)NodeExecutionStatus.Running;
            activation.Attempt = checked(activation.Attempt + 1);
            if (instance.Status is (int)WorkflowInstanceStatus.Created or (int)WorkflowInstanceStatus.Waiting)
            {
                instance.Status = (int)WorkflowInstanceStatus.Running;
                instance.Revision = checked(instance.Revision + 1);
                instance.UpdatedAtUnixMilliseconds = now;
                AddAudit(context, instance.Id, "InstanceRunning", instance.Revision, now);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var definition = PublishedWorkflowDefinitionReader.Read(
                definitionRow.CanonicalJson, definitionRow.DefinitionId, definitionRow.Version, definitionRow.Sha256);
            var state = WorkflowState.Create(instance.StateJson, instance.StateSchemaVersion);
            return StoreResults.Succeeded(new ClaimedWork(ToLease(item, instance.Revision, now), definition, state, activation.Attempt));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<WorkLease>> RenewLeaseAsync(
        RenewLeaseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsWholePositiveMilliseconds(command.LeaseDuration))
        {
            return StoreResults.Conflict<WorkLease>("EW3006_INVALID_LEASE_DURATION");
        }

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var item = await context.WorkItems.FindAsync([FormatGuid(command.WorkItemId.Value)], cancellationToken).ConfigureAwait(false);
            if (item is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<WorkLease>("EW3008_WORK_NOT_FOUND");
            }

            if (!IsCurrentLease(item, command.Token, command.Generation, now))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<WorkLease>("EW3009_STALE_LEASE");
            }

            var instance = await context.Instances.FindAsync([item.InstanceId], cancellationToken).ConfigureAwait(false);
            if (instance is null || instance.Status != (int)WorkflowInstanceStatus.Running)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<WorkLease>("EW3010_INSTANCE_NOT_EXECUTABLE");
            }

            item.LeaseExpiresAtUnixMilliseconds = checked(now + (long)command.LeaseDuration.TotalMilliseconds);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(ToLease(item, instance.Revision, now));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<CommitNodeResult>> CommitNodeResultAsync(
        CommitNodeResultCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var shapeError = ValidateCommitShape(command);
        if (shapeError is not null)
        {
            return StoreResults.Conflict<CommitNodeResult>(shapeError);
        }

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var item = await context.WorkItems.FindAsync([FormatGuid(command.WorkItemId.Value)], cancellationToken).ConfigureAwait(false);
            if (item is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<CommitNodeResult>("EW3008_WORK_NOT_FOUND");
            }

            if (!IsCurrentLease(item, command.Token, command.Generation, now))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<CommitNodeResult>("EW3009_STALE_LEASE");
            }

            var instance = await context.Instances.FindAsync([item.InstanceId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The committed work item has no instance.");
            if (instance.Status != (int)WorkflowInstanceStatus.Running || instance.Revision != command.ExpectedInstanceRevision)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<CommitNodeResult>("EW3011_INSTANCE_REVISION_CONFLICT");
            }

            var activation = await context.Activations.FindAsync([item.ActivationId], cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The committed work item has no activation.");
            if (command.Kind is NodeCommitKind.Retry && command.NextWork!.NodeId.Value != item.NodeId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<CommitNodeResult>("EW3015_RETRY_NODE_MISMATCH");
            }

            instance.Revision = checked(instance.Revision + 1);
            instance.UpdatedAtUnixMilliseconds = now;
            if (command.ReplacementState is not null)
            {
                instance.StateSchemaVersion = command.ReplacementState.SchemaVersion;
                instance.StateJson = command.ReplacementState.Value.CanonicalText;
            }

            ApplyCommit(context, command, item, activation, instance, now);
            AddOutbox(context, command, item, now);
            AddAudit(context, instance.Id, "NodeCommitted", instance.Revision, now);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new CommitNodeResult((WorkflowInstanceStatus)instance.Status, instance.Revision));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<CancelInstanceResult>> CancelInstanceAsync(
        CancelInstanceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsUtcMillisecond(command.RequestedAtUtc))
        {
            return StoreResults.Conflict<CancelInstanceResult>("EW3002_INVALID_REQUEST_TIME");
        }

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var instanceKey = FormatGuid(command.InstanceId.Value);
            var instance = await context.Instances.FindAsync([instanceKey], cancellationToken).ConfigureAwait(false);
            if (instance is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<CancelInstanceResult>("EW3012_INSTANCE_NOT_FOUND");
            }

            if (instance.Revision != command.ExpectedRevision || IsTerminal((WorkflowInstanceStatus)instance.Status))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<CancelInstanceResult>("EW3011_INSTANCE_REVISION_CONFLICT");
            }

            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            instance.Status = (int)WorkflowInstanceStatus.Cancelled;
            instance.Revision = checked(instance.Revision + 1);
            instance.UpdatedAtUnixMilliseconds = now;
            var workItems = await context.WorkItems
                .Where(row => row.InstanceId == instanceKey && row.Status != (int)WorkItemStatus.Done)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var workItem in workItems)
            {
                workItem.Status = (int)WorkItemStatus.Cancelled;
                ClearLease(workItem);
            }

            var activations = await context.Activations
                .Where(row => row.InstanceId == instanceKey &&
                    row.Status != (int)NodeExecutionStatus.Succeeded &&
                    row.Status != (int)NodeExecutionStatus.Failed)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var activation in activations)
            {
                activation.Status = (int)NodeExecutionStatus.Cancelled;
            }

            AddAudit(context, instance.Id, "InstanceCancelled", instance.Revision, now, command.Actor);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new CancelInstanceResult(instance.Revision));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<OutboxLease>> ClaimDueOutboxAsync(
        ClaimDueOutboxCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsWholePositiveMilliseconds(command.LeaseDuration))
        {
            return StoreResults.Conflict<OutboxLease>("EW3020_INVALID_OUTBOX_LEASE_DURATION");
        }

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var row = await context.Outbox
                .Where(item =>
                    (item.Status == (int)OutboxMessageStatus.Ready && item.DueAtUnixMilliseconds <= now) ||
                    (item.Status == (int)OutboxMessageStatus.Leased && item.LeaseExpiresAtUnixMilliseconds <= now))
                .OrderBy(item => item.DueAtUnixMilliseconds)
                .ThenBy(item => item.CreatedAtUnixMilliseconds)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<OutboxLease>("EW3021_NO_DUE_OUTBOX");
            }

            var token = Guid.NewGuid();
            row.Status = (int)OutboxMessageStatus.Leased;
            row.OwnerId = command.OwnerId.Value;
            row.Generation = checked(row.Generation + 1);
            row.Attempt = checked(row.Attempt + 1);
            row.LeaseToken = FormatGuid(token);
            row.LeaseExpiresAtUnixMilliseconds = checked(now + (long)command.LeaseDuration.TotalMilliseconds);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(ToOutboxLease(row, token, now));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<StoreResult<CommitOutboxResult>> CommitOutboxAsync(
        CommitOutboxCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if ((command.Kind is OutboxCommitKind.Retry && (command.RetryAtUtc is null || !IsUtcMillisecond(command.RetryAtUtc.Value))) ||
            (command.Kind is not OutboxCommitKind.Retry && command.RetryAtUtc is not null))
        {
            return StoreResults.Conflict<CommitOutboxResult>("EW3022_INVALID_OUTBOX_COMMIT");
        }

        return await ExecuteAsync(async context =>
        {
            await using var transaction = await BeginWriteTransactionAsync(context, cancellationToken).ConfigureAwait(false);
            var now = await ReadStoreUtcNowMillisecondsAsync(context, transaction, cancellationToken).ConfigureAwait(false);
            var row = await context.Outbox.FindAsync([FormatGuid(command.MessageId.Value)], cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.NotFound<CommitOutboxResult>("EW3023_OUTBOX_NOT_FOUND");
            }

            if (!IsCurrentOutboxLease(row, command.Token, command.Generation, now))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return StoreResults.Conflict<CommitOutboxResult>("EW3024_STALE_OUTBOX_LEASE");
            }

            if (command.Kind is OutboxCommitKind.Delivered)
            {
                row.Status = (int)OutboxMessageStatus.Delivered;
                row.DeliveredAtUnixMilliseconds = now;
                row.LastErrorCode = null;
            }
            else if (command.Kind is OutboxCommitKind.Retry)
            {
                row.Status = (int)OutboxMessageStatus.Ready;
                row.DueAtUnixMilliseconds = command.RetryAtUtc!.Value.ToUnixTimeMilliseconds();
                row.LastErrorCode = StoreContractRules.NormalizeOptionalText(command.ErrorCode);
            }
            else
            {
                row.Status = (int)OutboxMessageStatus.Failed;
                row.LastErrorCode = StoreContractRules.NormalizeOptionalText(command.ErrorCode);
            }

            ClearOutboxLease(row);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(new CommitOutboxResult((OutboxMessageStatus)row.Status, row.Attempt));
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyCommit(
        SqliteWorkflowDbContext context,
        CommitNodeResultCommand command,
        WorkItemRow item,
        ActivationRow activation,
        InstanceRow instance,
        long now)
    {
        switch (command.Kind)
        {
            case NodeCommitKind.Continue:
                item.Status = (int)WorkItemStatus.Done;
                ClearLease(item);
                activation.Status = (int)NodeExecutionStatus.Succeeded;
                instance.Status = (int)WorkflowInstanceStatus.Running;
                AddNextWork(context, instance.Id, command.NextWork!, now);
                break;
            case NodeCommitKind.Retry:
                item.Status = (int)WorkItemStatus.Ready;
                item.DueAtUnixMilliseconds = command.NextWork!.DueAtUtc.ToUnixTimeMilliseconds();
                ClearLease(item);
                activation.Status = (int)NodeExecutionStatus.Pending;
                activation.ErrorCode = StoreContractRules.NormalizeOptionalText(command.ErrorCode);
                instance.Status = (int)WorkflowInstanceStatus.Running;
                break;
            case NodeCommitKind.Complete:
                item.Status = (int)WorkItemStatus.Done;
                ClearLease(item);
                activation.Status = (int)NodeExecutionStatus.Succeeded;
                instance.Status = (int)WorkflowInstanceStatus.Completed;
                break;
            case NodeCommitKind.Fail:
                item.Status = (int)WorkItemStatus.Done;
                ClearLease(item);
                activation.Status = (int)NodeExecutionStatus.Failed;
                activation.ErrorCode = StoreContractRules.NormalizeOptionalText(command.ErrorCode);
                instance.Status = (int)WorkflowInstanceStatus.Failed;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Unknown commit kind.");
        }
    }

    private static void AddOutbox(
        SqliteWorkflowDbContext context,
        CommitNodeResultCommand command,
        WorkItemRow item,
        long now)
    {
        foreach (var write in command.Outbox ?? [])
        {
            context.Outbox.Add(new OutboxRow
            {
                Id = FormatGuid(Guid.NewGuid()),
                InstanceId = item.InstanceId,
                ActivationId = item.ActivationId,
                OperationId = write.OperationId.Value,
                Destination = write.Destination.Value,
                ContentType = write.ContentType,
                PayloadJson = write.PayloadJson,
                IdempotencyKey = ComputeOutboxIdempotencyKey(item.InstanceId, item.ActivationId, write.OperationId.Value),
                Status = (int)OutboxMessageStatus.Ready,
                Attempt = 0,
                DueAtUnixMilliseconds = now,
                Generation = 0,
                CreatedAtUnixMilliseconds = now,
            });
        }
    }

    private static void AddNextWork(SqliteWorkflowDbContext context, string instanceId, NextWork nextWork, long now)
    {
        var activationId = Guid.NewGuid();
        var activationKey = FormatGuid(activationId);
        context.Activations.Add(new ActivationRow
        {
            Id = activationKey,
            InstanceId = instanceId,
            NodeId = nextWork.NodeId.Value,
            Status = (int)NodeExecutionStatus.Pending,
            Attempt = 0,
        });
        context.WorkItems.Add(new WorkItemRow
        {
            Id = FormatGuid(Guid.NewGuid()),
            ActivationId = activationKey,
            InstanceId = instanceId,
            NodeId = nextWork.NodeId.Value,
            Status = (int)WorkItemStatus.Ready,
            DueAtUnixMilliseconds = nextWork.DueAtUtc.ToUnixTimeMilliseconds(),
            Generation = 0,
            CreatedAtUnixMilliseconds = now,
        });
    }

    private static string? ValidateCommitShape(CommitNodeResultCommand command)
    {
        if (command.Kind is NodeCommitKind.Continue or NodeCommitKind.Retry)
        {
            if (command.NextWork is null || !IsUtcMillisecond(command.NextWork.DueAtUtc))
            {
                return "EW3013_NEXT_WORK_REQUIRED";
            }
        }
        else if (command.NextWork is not null)
        {
            return "EW3014_NEXT_WORK_NOT_ALLOWED";
        }

        if ((command.Outbox?.Count ?? 0) > 0 && command.Kind is NodeCommitKind.Retry or NodeCommitKind.Fail)
        {
            return "EW3017_OUTBOX_REQUIRES_SUCCESS";
        }

        if (command.Outbox is not null && command.Outbox
            .GroupBy(item => item.OperationId.Value, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            return "EW3018_DUPLICATE_OUTBOX_OPERATION";
        }

        if (command.Outbox is not null && command.Outbox.Any(item => !StoreContractRules.IsValidOutboxWrite(item)))
        {
            return "EW3019_INVALID_OUTBOX_WRITE";
        }

        return null;
    }

    private static async Task<StartReceiptRow?> FindReceiptAsync(
        SqliteWorkflowDbContext context,
        StartInstanceCommand command,
        CancellationToken cancellationToken) =>
        await context.StartReceipts.FindAsync(
            [StoreContractRules.ComputeStartReceiptKey(command.Scope, command.IdempotencyKey)],
            cancellationToken).ConfigureAwait(false);

    private static bool ReceiptMatches(StartReceiptRow receipt, StartInstanceCommand command) =>
        string.Equals(receipt.InstallationId, command.Scope.InstallationId.Value, StringComparison.Ordinal) &&
        string.Equals(receipt.CommandTypeId, command.Scope.CommandTypeId.Value, StringComparison.Ordinal) &&
        string.Equals(receipt.ActorProviderId, command.Scope.Actor.ProviderId.Value, StringComparison.Ordinal) &&
        string.Equals(receipt.ActorSubjectId, command.Scope.Actor.SubjectId, StringComparison.Ordinal) &&
        string.Equals(receipt.IdempotencyKey, command.IdempotencyKey.Value, StringComparison.Ordinal);

    private static async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginWriteTransactionAsync(
        SqliteWorkflowDbContext context,
        CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        var transaction = connection.BeginTransaction(deferred: false);
        await context.Database.UseTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
        return context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("EF Core did not attach the SQLite transaction.");
    }

    private static async Task<long> ReadStoreUtcNowMillisecondsAsync(
        SqliteWorkflowDbContext context,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText =
            "SELECT CAST(strftime('%s','now') AS INTEGER) * 1000 + CAST(substr(strftime('%f','now'), 4, 3) AS INTEGER);";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<StoreResult<T>> ExecuteAsync<T>(
        Func<SqliteWorkflowDbContext, Task<StoreResult<T>>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = _database.CreateDbContext();
            return await operation(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqliteException exception)
        {
            return StoreResults.Unavailable<T>($"EW3090_SQLITE_{exception.SqliteErrorCode}");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException sqliteException)
        {
            return StoreResults.Unavailable<T>($"EW3090_SQLITE_{sqliteException.SqliteErrorCode}");
        }
    }

    private static WorkLease ToLease(WorkItemRow item, long instanceRevision, long now) =>
        new(
            new WorkItemId(ParseGuid(item.Id)),
            new NodeActivationId(ParseGuid(item.ActivationId)),
            new WorkflowInstanceId(ParseGuid(item.InstanceId)),
            new TechnicalId(item.NodeId),
            new TechnicalId(item.OwnerId!),
            item.Generation,
            new LeaseToken(ParseGuid(item.LeaseToken!)),
            DateTimeOffset.FromUnixTimeMilliseconds(now),
            DateTimeOffset.FromUnixTimeMilliseconds(item.LeaseExpiresAtUnixMilliseconds!.Value),
            instanceRevision);

    private static OutboxLease ToOutboxLease(OutboxRow row, Guid token, long now) => new(
        new OutboxMessageId(ParseGuid(row.Id)),
        new WorkflowInstanceId(ParseGuid(row.InstanceId)),
        new NodeActivationId(ParseGuid(row.ActivationId)),
        new TechnicalId(row.OperationId),
        new TechnicalId(row.Destination),
        row.ContentType,
        row.PayloadJson,
        row.IdempotencyKey,
        row.Attempt,
        row.Generation,
        new LeaseToken(token),
        DateTimeOffset.FromUnixTimeMilliseconds(now),
        DateTimeOffset.FromUnixTimeMilliseconds(row.LeaseExpiresAtUnixMilliseconds!.Value));

    private static bool IsCurrentLease(WorkItemRow item, LeaseToken token, long generation, long now) =>
        item.Status == (int)WorkItemStatus.Leased &&
        item.Generation == generation &&
        string.Equals(item.LeaseToken, FormatGuid(token.Value), StringComparison.Ordinal) &&
        item.LeaseExpiresAtUnixMilliseconds > now;

    private static bool IsCurrentOutboxLease(OutboxRow row, LeaseToken token, long generation, long now) =>
        row.Status == (int)OutboxMessageStatus.Leased &&
        row.Generation == generation &&
        string.Equals(row.LeaseToken, FormatGuid(token.Value), StringComparison.Ordinal) &&
        row.LeaseExpiresAtUnixMilliseconds > now;

    private static void ClearLease(WorkItemRow item)
    {
        item.OwnerId = null;
        item.LeaseToken = null;
        item.LeaseExpiresAtUnixMilliseconds = null;
    }

    private static void ClearOutboxLease(OutboxRow row)
    {
        row.OwnerId = null;
        row.LeaseToken = null;
        row.LeaseExpiresAtUnixMilliseconds = null;
    }

    private static string ComputeOutboxIdempotencyKey(string instanceId, string activationId, string operationId) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{instanceId}:{activationId}:{operationId}")));

    private static void AddAudit(
        SqliteWorkflowDbContext context,
        string instanceId,
        string eventType,
        long revision,
        long now,
        ActorIdentity? actor = null) =>
        context.Audits.Add(new AuditRow
        {
            InstanceId = instanceId,
            EventType = eventType,
            Revision = revision,
            OccurredAtUnixMilliseconds = now,
            ActorProviderId = actor?.ProviderId.Value,
            ActorSubjectId = actor?.SubjectId,
        });

    private static string ReadStartNodeId(string canonicalJson)
    {
        using var document = JsonDocument.Parse(canonicalJson);
        foreach (var node in document.RootElement.GetProperty("nodes").EnumerateArray())
        {
            if (string.Equals(node.GetProperty("role").GetString(), "Start", StringComparison.Ordinal))
            {
                return node.GetProperty("id").GetString()
                    ?? throw new InvalidOperationException("The published start node has no identifier.");
            }
        }

        throw new InvalidOperationException("The published definition has no start node.");
    }

    private static bool IsWholePositiveMilliseconds(TimeSpan value) =>
        value > TimeSpan.Zero && value.Ticks % TimeSpan.TicksPerMillisecond == 0;

    private static bool IsUtcMillisecond(DateTimeOffset value) => StoreContractRules.HasUtcMillisecondPrecision(value);

    private static bool IsTerminal(WorkflowInstanceStatus status) =>
        status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Failed or WorkflowInstanceStatus.Cancelled;

    private static string FormatGuid(Guid value) => value.ToString("N", System.Globalization.CultureInfo.InvariantCulture);

    private static Guid ParseGuid(string value) => Guid.ParseExact(value, "N");
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Sqlite;

public sealed partial class SqliteWorkflowStore
{
    public async ValueTask<StoreResult<IReadOnlyList<WorkflowMediaRecord>>> ExpireStagedMediaAsync(DateTimeOffset olderThanUtc, int limit, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            if (limit is < 1 or > 1000) return StoreResults.Conflict<IReadOnlyList<WorkflowMediaRecord>>("store.media-purge-limit");
            var cutoff = olderThanUtc.ToUnixTimeMilliseconds();
            var rows = await context.Set<WorkflowMediaRow>().Where(row => (row.Status == (int)WorkflowMediaStatus.Staged || row.Status == (int)WorkflowMediaStatus.Abandoned || row.Status == (int)WorkflowMediaStatus.Quarantined) && row.CreatedAtMs < cutoff)
                .OrderBy(row => row.CreatedAtMs).Take(limit).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            foreach (var row in rows) row.Status = (int)WorkflowMediaStatus.Abandoned;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<WorkflowMediaRecord> expired = rows.Select(ToMedia).ToArray();
            return StoreResults.Succeeded(expired);
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<bool>> AcknowledgeMediaPurgeAsync(WorkflowMediaReference reference, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var row = await context.Set<WorkflowMediaRow>().FindAsync([MediaKey(reference)], cancellationToken).ConfigureAwait(false);
            if (row is null || row.Status != (int)WorkflowMediaStatus.Abandoned) return StoreResults.Conflict<bool>("store.media-purge-conflict");
            row.Status = (int)WorkflowMediaStatus.Deleted;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(true);
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<bool>> CompleteMediaInspectionAsync(CompleteWorkflowMediaInspectionCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var row = await context.Set<WorkflowMediaRow>().FindAsync([MediaKey(command.Reference)], cancellationToken).ConfigureAwait(false);
            if (row is null || row.Status != (int)WorkflowMediaStatus.Quarantined || !string.Equals(row.Sha256, command.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                return StoreResults.Conflict<bool>("store.media-inspection-conflict");
            row.Status = command.Approved ? (int)WorkflowMediaStatus.Staged : (int)WorkflowMediaStatus.Deleted;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(true);
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<WorkflowMediaRecord>> StageMediaAsync(StageWorkflowMediaCommand command, CancellationToken cancellationToken)
    {
        var media = command.Media;
        if (media.Reference.Id == Guid.Empty || media.Reference.Revision < 1 || media.Length < 0 || media.Length > 100 * 1024 * 1024 ||
            media.Name.Length is 0 or > 256 || media.ContentType.Length is 0 or > 128 || media.ObjectKey.Length > 128 || !StoreContractRules.IsSha256(media.Sha256) ||
            media.Status is not (WorkflowMediaStatus.Staged or WorkflowMediaStatus.Quarantined) || JsonSerializer.Serialize(media.PermissionGroups).Length > 4000 ||
            media.SubmissionKey?.Length > 128 || media.SourceField?.Length > 128)
            return StoreResults.Conflict<WorkflowMediaRecord>("store.media-invalid");
        return await ExecuteAsync(async context =>
        {
            var key = MediaKey(media.Reference);
            var existing = await context.Set<WorkflowMediaRow>().FindAsync([key], cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return existing.Sha256 == media.Sha256 && existing.OwnerProviderId == media.Owner.ProviderId.Value && existing.OwnerSubjectId == media.Owner.SubjectId &&
                    existing.InstanceId == (media.InstanceId is { } instance ? FormatGuid(instance.Value) : null) &&
                    existing.ActivationId == (media.ActivationId is { } activation ? FormatGuid(activation.Value) : null) && existing.SubmissionKey == media.SubmissionKey && existing.Name == media.Name &&
                    existing.SourceField == media.SourceField && existing.Hidden == (media.Hidden ? 1 : 0) && existing.PermissionGroupsJson == JsonSerializer.Serialize(media.PermissionGroups) &&
                    existing.ContentType == media.ContentType && existing.Length == media.Length &&
                    existing.Status is (int)WorkflowMediaStatus.Staged or (int)WorkflowMediaStatus.Active or (int)WorkflowMediaStatus.Superseded or (int)WorkflowMediaStatus.Quarantined
                    ? StoreResults.Idempotent(ToMedia(existing)) : StoreResults.Conflict<WorkflowMediaRecord>("store.media-operation-conflict");
            if (media.Reference.Revision > 1)
            {
                var previous = await context.Set<WorkflowMediaRow>().FindAsync([MediaKey(media.Reference with { Revision = media.Reference.Revision - 1 })], cancellationToken).ConfigureAwait(false);
                if (previous is null || previous.Status != (int)WorkflowMediaStatus.Active || previous.InstanceId != (media.InstanceId is { } owner ? FormatGuid(owner.Value) : null))
                    return StoreResults.Conflict<WorkflowMediaRecord>("store.media-revision-conflict");
            }
            context.Add(new WorkflowMediaRow { Key = key, MediaId = FormatGuid(media.Reference.Id), Revision = media.Reference.Revision, Name = media.Name,
                ContentType = media.ContentType, Length = media.Length, Sha256 = media.Sha256, ObjectKey = media.ObjectKey, OwnerProviderId = media.Owner.ProviderId.Value,
                OwnerSubjectId = media.Owner.SubjectId, InstanceId = media.InstanceId is { } iid ? FormatGuid(iid.Value) : null,
                ActivationId = media.ActivationId is { } aid ? FormatGuid(aid.Value) : null, SubmissionKey = media.SubmissionKey, SourceField = media.SourceField,
                Hidden = media.Hidden ? 1 : 0, PermissionGroupsJson = JsonSerializer.Serialize(media.PermissionGroups), Status = (int)media.Status,
                CreatedAtMs = media.CreatedAtUtc.ToUnixTimeMilliseconds() });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(media);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<StoreResult<WorkflowMediaRecord>> ReadMediaAsync(WorkflowMediaReference reference, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var row = await context.Set<WorkflowMediaRow>().FindAsync([MediaKey(reference)], cancellationToken).ConfigureAwait(false);
            return row is null ? StoreResults.NotFound<WorkflowMediaRecord>("store.media-unavailable") : StoreResults.Succeeded(ToMedia(row));
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<IReadOnlyList<WorkflowMediaRecord>>> ListMediaAsync(WorkflowInstanceId instanceId, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            var id = FormatGuid(instanceId.Value);
            var rows = await context.Set<WorkflowMediaRow>().AsNoTracking().Where(row => row.InstanceId == id && row.Status == (int)WorkflowMediaStatus.Active)
                .OrderByDescending(row => row.CreatedAtMs).Take(1001).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (rows.Length > 1000) return StoreResults.Conflict<IReadOnlyList<WorkflowMediaRecord>>("store.media-library-limit");
            IReadOnlyList<WorkflowMediaRecord> media = rows.Select(ToMedia).ToArray();
            return StoreResults.Succeeded(media);
        }, cancellationToken).ConfigureAwait(false);

    public async ValueTask<StoreResult<bool>> DeleteMediaAsync(DeleteWorkflowMediaCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async context =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            var id = FormatGuid(command.Reference.Id);
            var instance = FormatGuid(command.InstanceId.Value);
            var rows = await context.Set<WorkflowMediaRow>().Where(row => row.MediaId == id && row.InstanceId == instance).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!rows.Any(row => row.Revision == command.Reference.Revision && row.Status == (int)WorkflowMediaStatus.Active)) return StoreResults.Conflict<bool>("store.media-revision-conflict");
            foreach (var row in rows) row.Status = (int)WorkflowMediaStatus.Deleted;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return StoreResults.Succeeded(true);
        }, cancellationToken).ConfigureAwait(false);

    private static async Task AttachMediaAsync(SqliteWorkflowDbContext context, IReadOnlyList<WorkflowMediaReference>? media,
        string instanceId, string? activationId, ActorIdentity? actor, string? submissionKey, CancellationToken token)
    {
        if (media is null) return;
        if (media.Count > 32 || media.Distinct().Count() != media.Count) throw new WorkflowMediaCommitException();
        foreach (var reference in media)
        {
            var row = await context.Set<WorkflowMediaRow>().FindAsync([MediaKey(reference)], token).ConfigureAwait(false);
            if (row is null || row.Status != (int)WorkflowMediaStatus.Staged ||
                (row.InstanceId is not null && row.InstanceId != instanceId) || row.ActivationId != activationId ||
                (actor is not null && (row.OwnerProviderId != actor.ProviderId.Value || row.OwnerSubjectId != actor.SubjectId)) ||
                (activationId is null && (row.SubmissionKey != submissionKey || submissionKey is null))) throw new WorkflowMediaCommitException();
            if (reference.Revision > 1)
            {
                var previous = await context.Set<WorkflowMediaRow>().FindAsync([MediaKey(reference with { Revision = reference.Revision - 1 })], token).ConfigureAwait(false);
                if (previous is null || previous.Status != (int)WorkflowMediaStatus.Active) throw new WorkflowMediaCommitException();
                previous.Status = (int)WorkflowMediaStatus.Superseded;
            }
            row.InstanceId = instanceId;
            row.Status = (int)WorkflowMediaStatus.Active;
        }
    }

    private static async Task DeleteCommittedMediaAsync(SqliteWorkflowDbContext context, IReadOnlyList<WorkflowMediaReference>? references, string instanceId, CancellationToken token)
    {
        if (references is null) return;
        if (references.Count > 32 || references.Distinct().Count() != references.Count) throw new WorkflowMediaCommitException();
        foreach (var reference in references)
        {
            var id = FormatGuid(reference.Id);
            var rows = await context.Set<WorkflowMediaRow>().Where(row => row.MediaId == id && row.InstanceId == instanceId).ToArrayAsync(token).ConfigureAwait(false);
            if (!rows.Any(row => row.Revision == reference.Revision && row.Status == (int)WorkflowMediaStatus.Active)) throw new WorkflowMediaCommitException();
            foreach (var row in rows) row.Status = (int)WorkflowMediaStatus.Deleted;
        }
    }

    private static string MediaKey(WorkflowMediaReference reference) => FormatGuid(reference.Id) + ":" + reference.Revision;
    private static WorkflowMediaRecord ToMedia(WorkflowMediaRow row) => new(new(ParseGuid(row.MediaId), row.Revision), row.Name, row.ContentType,
        row.Length, row.Sha256, row.ObjectKey, new(new(row.OwnerProviderId), row.OwnerSubjectId), row.InstanceId is null ? null : new(ParseGuid(row.InstanceId)),
        row.ActivationId is null ? null : new(ParseGuid(row.ActivationId)), row.SubmissionKey, row.SourceField, row.Hidden != 0,
        JsonSerializer.Deserialize<string[][]>(row.PermissionGroupsJson) ?? [], (WorkflowMediaStatus)row.Status, DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAtMs));
}

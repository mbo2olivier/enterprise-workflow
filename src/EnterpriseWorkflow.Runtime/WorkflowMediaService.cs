using System.Security.Cryptography;
using System.Text;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

/// <summary>SDK service for trusted handlers. References are made visible only by a successful source commit.</summary>
public sealed class WorkflowMediaService(IWorkflowStore store, IWorkflowMediaStorageProvider storage,
    IEnumerable<IWorkflowMediaScanner> scanners, TimeProvider? timeProvider = null, IEnumerable<IWorkflowMediaInspector>? inspectors = null, IWorkflowMediaFormatResolver? formatResolver = null) : IWorkflowMediaService
{
    private readonly IWorkflowMediaInspector? _inspector = inspectors?.SingleOrDefault();
    public async ValueTask<bool> CompleteInspectionAsync(WorkflowMediaReference reference, string expectedSha256, bool approved, CancellationToken cancellationToken) =>
        (await store.CompleteMediaInspectionAsync(new(reference, expectedSha256, approved), cancellationToken)).Outcome == StoreOutcome.Succeeded;

    public async ValueTask<int> PurgeExpiredStagingAsync(TimeSpan minimumAge, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumAge, TimeSpan.FromHours(1));
        var result = await store.ExpireStagedMediaAsync((timeProvider ?? TimeProvider.System).GetUtcNow() - minimumAge, batchSize, cancellationToken);
        if (result.Outcome != StoreOutcome.Succeeded || result.Value is null) throw new InvalidOperationException("Media cleanup unavailable.");
        var deleted = 0;
        foreach (var item in result.Value)
        {
            await storage.DeleteAsync(item.ObjectKey, cancellationToken);
            if ((await store.AcknowledgeMediaPurgeAsync(item.Reference, cancellationToken)).Outcome == StoreOutcome.Succeeded) deleted++;
        }
        return deleted;
    }
    public async ValueTask<WorkflowMediaRecord> StageAsync(WorkflowMediaWriteScope scope, string operationId, string fileName,
        Stream content, WorkflowMediaSpecification specification, WorkflowMediaVisibility visibility, CancellationToken cancellationToken,
        WorkflowMediaReference? replace = null)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 128 || specification.MaximumBytes is < 1 or > 100 * 1024 * 1024 ||
            (scope.ActivationId is null && string.IsNullOrEmpty(scope.SubmissionKey))) throw new ArgumentException("Invalid media scope or specification.");
        var name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 256 || name.Any(char.IsControl)) throw new InvalidDataException("Invalid media file name.");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!specification.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("File extension not allowed.");
        using var buffer = new MemoryStream();
        var block = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(block, cancellationToken)) > 0)
        {
            if (buffer.Length + read > specification.MaximumBytes) throw new InvalidDataException("File exceeds media size limit.");
            buffer.Write(block, 0, read);
        }
        var bytes = buffer.ToArray();
        var declared = specification.Formats?.SingleOrDefault(format => format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        string detected;
        if (declared is { BuiltIn: false })
        {
            if (scope.Artifact is null || formatResolver is null) throw new InvalidDataException("Custom format validator is unavailable.");
            using var source = new MemoryStream(bytes, false);
            if (!await formatResolver.ValidateAsync(scope.Artifact, declared, source, cancellationToken)) throw new InvalidDataException("Custom media format rejected the file.");
            detected = declared.ContentType;
        }
        else detected = WorkflowBuiltInMediaValidator.Detect(bytes, extension, cancellationToken);
        if (!specification.AllowedContentTypes.Contains(detected, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Detected file type not allowed.");
        foreach (var scanner in scanners)
        {
            using var inspectedContent = new MemoryStream(bytes, false);
            if (!await scanner.ApproveAsync(inspectedContent, detected, cancellationToken)) throw new InvalidDataException("Media validation rejected the file.");
        }
        var scopeKey = $"{scope.Owner.ProviderId.Value}:{scope.Owner.SubjectId}:{scope.InstanceId?.Value:N}:{scope.ActivationId?.Value:N}:{scope.SubmissionKey}:{operationId}";
        var identityHash = SHA256.HashData(Encoding.UTF8.GetBytes(scopeKey));
        var reference = replace is null ? new WorkflowMediaReference(new Guid(identityHash.AsSpan(0, 16)), 1) : replace with { Revision = checked(replace.Revision + 1) };
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var objectKey = reference.Id.ToString("N") + "-" + reference.Revision + "-" + sha256;
        buffer.Position = 0;
        await storage.WriteAsync(objectKey, buffer, specification.MaximumBytes, cancellationToken);
        var media = new WorkflowMediaRecord(reference, name, detected, bytes.Length, sha256, objectKey, scope.Owner,
            scope.InstanceId, scope.ActivationId, scope.SubmissionKey, scope.SourceField, visibility.Hidden, visibility.PermissionGroups,
            _inspector is null ? WorkflowMediaStatus.Staged : WorkflowMediaStatus.Quarantined, (timeProvider ?? TimeProvider.System).GetUtcNow());
        var staged = await store.StageMediaAsync(new(media), cancellationToken);
        if (staged.Outcome is not (StoreOutcome.Succeeded or StoreOutcome.Idempotent) || staged.Value is null)
            throw new InvalidOperationException(staged.ErrorCode ?? "Media could not be staged.");
        if (_inspector is not null && staged.Value.Status == WorkflowMediaStatus.Quarantined)
        {
            using var inspectedContent = new MemoryStream(bytes, false);
            var inspection = await _inspector.InspectAsync(inspectedContent, staged.Value, cancellationToken);
            if (!Enum.IsDefined(inspection) || inspection == WorkflowMediaInspectionDecision.Rejected)
            {
                await CompleteInspectionAsync(reference, sha256, false, cancellationToken);
                throw new InvalidDataException("Media inspection rejected the file.");
            }
            if (inspection == WorkflowMediaInspectionDecision.Approved) await CompleteInspectionAsync(reference, sha256, true, cancellationToken);
            var inspected = await store.ReadMediaAsync(reference, cancellationToken);
            return inspected.Value is { Status: WorkflowMediaStatus.Staged or WorkflowMediaStatus.Quarantined or WorkflowMediaStatus.Active } value
                ? value : throw new InvalidOperationException("Media inspection status unavailable.");
        }
        return staged.Value;
    }

    public async ValueTask<WorkflowMediaRecord?> GetStagedAsync(WorkflowMediaReference reference, CancellationToken cancellationToken) =>
        (await store.ReadMediaAsync(reference, cancellationToken)).Value;
    public async ValueTask<WorkflowMediaRecord?> GetForInstanceAsync(WorkflowInstanceId instanceId, WorkflowMediaReference reference, CancellationToken cancellationToken)
    {
        var result = await store.ReadMediaAsync(reference, cancellationToken);
        return result.Value is { Status: WorkflowMediaStatus.Active or WorkflowMediaStatus.Superseded } item && item.InstanceId == instanceId ? item : null;
    }
    public async ValueTask<bool> DeleteForInstanceAsync(WorkflowInstanceId instanceId, WorkflowMediaReference reference, CancellationToken cancellationToken)
    {
        var result = await store.DeleteMediaAsync(new(reference, instanceId), cancellationToken);
        return result.Outcome == StoreOutcome.Succeeded;
    }
    public async ValueTask<Stream> OpenForInstanceAsync(WorkflowInstanceId instanceId, WorkflowMediaReference reference, CancellationToken cancellationToken)
    {
        var result = await store.ReadMediaAsync(reference, cancellationToken);
        if (result.Value is not { } media || media.InstanceId != instanceId || media.Status is not (WorkflowMediaStatus.Active or WorkflowMediaStatus.Superseded))
            throw new FileNotFoundException("Media unavailable.");
        return await storage.OpenAsync(media.ObjectKey, cancellationToken);
    }

}

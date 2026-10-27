using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Presentation;

public sealed record WorkflowMediaUploadResult(WorkflowMediaReference Reference, bool Pending);

public sealed record WorkflowMediaItem(WorkflowMediaReference Reference, string Name, string ContentType, long Length, DateTimeOffset CreatedAtUtc);

public sealed class WorkflowMediaPortalService(IWorkflowStore store, IWorkflowMediaService media,
    IWorkflowPortalService portal, IWorkflowPresentationCatalog catalog, IWorkflowAuthorizationService authorization,
    IAuthorizationProvider generalAuthorization, IHumanTaskService tasks)
{
    public async ValueTask<WorkflowMediaUploadResult?> UploadStartAsync(IdentityReference actor, string processId, int version,
        string fieldId, string submissionKey, string operationId, string name, Stream content, CancellationToken token)
    {
        if (submissionKey.Length is < 1 or > 128 || !catalog.TryGetProcess(processId, version, out var process) || process is null ||
            !(await authorization.AuthorizeAsync(new(actor, new(processId, version, null, WorkflowActions.Start)), token)).Allowed ||
            !catalog.TryResolveForm(process.Definition, process.StartForm, out var form) || form is null) return null;
        return await Stage(form.Form, new(new(new(actor.ProviderId), actor.SubjectId), null, null, submissionKey, fieldId, form.Artifact),
            fieldId, operationId, name, content, token);
    }

    public async ValueTask<WorkflowMediaUploadResult?> UploadTaskAsync(IdentityReference actor, HumanTaskId taskId, string fieldId,
        string operationId, string name, Stream content, CancellationToken token)
    {
        var task = await tasks.ReadAsync(taskId, actor, token);
        var view = await portal.ReadTaskAsync(actor, taskId, token);
        if (task.Snapshot is not { } snapshot || view is null || view.Actions.Count == 0 ||
            snapshot.Assignee != new ActorIdentity(new(actor.ProviderId), actor.SubjectId) || snapshot.Status is not (HumanTaskStatus.Assigned or HumanTaskStatus.Claimed)) return null;
        if (!catalog.TryResolveForm(snapshot.Definition, view.Form.Reference, out var registration) || registration is null) return null;
        return await Stage(view.Form, new(new(new(actor.ProviderId), actor.SubjectId), snapshot.InstanceId, snapshot.ActivationId, SourceField: fieldId, Artifact: registration.Artifact),
            fieldId, operationId, name, content, token);
    }

    private async ValueTask<WorkflowMediaUploadResult?> Stage(WorkflowFormDefinition form, WorkflowMediaWriteScope scope, string fieldId,
        string operationId, string name, Stream content, CancellationToken token)
    {
        var field = form.Fields.SingleOrDefault(field => field.Id.Value == fieldId && field.Kind == WorkflowFormFieldKind.File);
        if (field?.FileSpecification is not { } spec) return null;
        var staged = await media.StageAsync(scope, operationId, name, content, new(spec.MaximumBytes, spec.AllowedContentTypes, spec.AllowedExtensions, spec.Policy?.Formats),
            spec.Policy is { } policy ? new(policy.Hidden, policy.PermissionGroups) : new(!field.ReadableBy.HasFlag(WorkflowFieldAudience.InstanceReader), []), token);
        return new(staged.Reference, staged.Status == WorkflowMediaStatus.Quarantined);
    }

    public async ValueTask<string?> InspectionStatusAsync(IdentityReference actor, WorkflowMediaReference reference, CancellationToken token)
    {
        var result = await store.ReadMediaAsync(reference, token);
        if (result.Value is not { } item || item.Owner != new ActorIdentity(new(actor.ProviderId), actor.SubjectId)) return null;
        return item.Status switch { WorkflowMediaStatus.Quarantined => "pending", WorkflowMediaStatus.Staged or WorkflowMediaStatus.Active => "ready", _ => "rejected" };
    }

    public async ValueTask<IReadOnlyList<WorkflowMediaItem>> ListAsync(IdentityReference actor, WorkflowInstanceId instanceId, CancellationToken token)
    {
        var detail = await portal.ReadInstanceAsync(actor, instanceId, token);
        if (detail is null) return [];
        var result = await store.ListMediaAsync(instanceId, token);
        if (result.Outcome != StoreOutcome.Succeeded || result.Value is null) throw new InvalidOperationException("Media library unavailable.");
        var accepted = new List<WorkflowMediaItem>();
        foreach (var item in result.Value)
            if (await CanRead(item, actor, token)) accepted.Add(new(item.Reference, item.Name, item.ContentType, item.Length, item.CreatedAtUtc));
        return accepted;
    }

    public async ValueTask<(WorkflowMediaItem Metadata, Stream Content)?> OpenAsync(IdentityReference actor, WorkflowMediaReference reference, CancellationToken token)
    {
        var result = await store.ReadMediaAsync(reference, token);
        if (result.Value is not { InstanceId: { } instanceId, Status: WorkflowMediaStatus.Active or WorkflowMediaStatus.Superseded } item) return null;
        var detail = await portal.ReadInstanceAsync(actor, instanceId, token);
        if (detail is null || !await CanRead(item, actor, token)) return null;
        return (new(item.Reference, item.Name, item.ContentType, item.Length, item.CreatedAtUtc), await media.OpenForInstanceAsync(instanceId, reference, token));
    }

    private async ValueTask<bool> CanRead(WorkflowMediaRecord item, IdentityReference actor, CancellationToken token)
    {
        if (item.Hidden) return false;
        foreach (var group in item.PermissionGroups)
        {
            var allowed = false;
            foreach (var permission in group)
                if ((await generalAuthorization.AuthorizeAsync(new(actor, new(permission)), token)).Allowed) { allowed = true; break; }
            if (!allowed) return false;
        }
        // Visibility belongs to the immutable media policy and instance authorization,
        // independently of whether business state stores a reference to this file.
        return true;
    }
}

using System.Text.Json;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Presentation;

public static class WorkflowMediaFormService
{
    public static IReadOnlyList<WorkflowMediaReference> References(WorkflowFormReference reference, IWorkflowPresentationCatalog catalog, WorkflowDefinition definition, CanonicalJson submission) =>
        catalog.TryResolveForm(definition, reference, out var registration) && registration is not null ? References(registration.Form, submission) : [];
    public static IReadOnlyList<WorkflowMediaReference> References(WorkflowFormDefinition form, CanonicalJson submission) =>
        form.Fields.Where(field => field.Kind == WorkflowFormFieldKind.File).SelectMany(field => FieldReferences(field, submission)).ToArray();
    public static IReadOnlyList<WorkflowMediaReference> FieldReferences(WorkflowFormField field, CanonicalJson submission) =>
        submission.ToJsonElement().TryGetProperty(field.Id.Value, out var value) ? value.Deserialize<WorkflowMediaReference[]>() ?? [] : [];

    public static async ValueTask<bool> ValidateAsync(IWorkflowStore store, WorkflowFormDefinition form, CanonicalJson submission,
        ActorIdentity actor, WorkflowInstanceId? instanceId, NodeActivationId? activationId, string? submissionKey, CancellationToken token)
    {
        foreach (var field in form.Fields.Where(field => field.Kind == WorkflowFormFieldKind.File))
        {
            foreach (var reference in FieldReferences(field, submission))
            {
                var result = await store.ReadMediaAsync(reference, token);
                if (result.Value is not { } media || media.Owner != actor || media.ActivationId != activationId ||
                    (instanceId is not null && media.InstanceId != instanceId) || media.SubmissionKey != submissionKey || media.SourceField != field.Id.Value ||
                    media.Status is not (WorkflowMediaStatus.Staged or WorkflowMediaStatus.Active) || field.FileSpecification is not { } spec || media.Length > spec.MaximumBytes ||
                    !spec.AllowedContentTypes.Contains(media.ContentType, StringComparer.OrdinalIgnoreCase) ||
                    !spec.AllowedExtensions.Contains(Path.GetExtension(media.Name), StringComparer.OrdinalIgnoreCase) ||
                    (spec.Policy is { } policy && (media.Hidden != policy.Hidden || JsonSerializer.Serialize(media.PermissionGroups) != JsonSerializer.Serialize(policy.PermissionGroups)))) return false;
            }
        }
        return true;
    }
}

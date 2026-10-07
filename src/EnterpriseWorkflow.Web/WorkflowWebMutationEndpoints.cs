using System.Globalization;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnterpriseWorkflow.Web;

internal static class WorkflowWebMutationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/workflow/start/{processId}/{version:int}", StartAsync).RequireAuthorization();
        endpoints.MapPost("/workflow/tasks/{taskId:guid}/action", CompleteTaskAsync).RequireAuthorization();
        endpoints.MapPost("/workflow/tasks/{taskId:guid}/claim", ClaimTaskAsync).RequireAuthorization();
        endpoints.MapPost("/workflow/tasks/{taskId:guid}/release", ReleaseTaskAsync).RequireAuthorization();
        endpoints.MapPost("/admin/workflows/grants", GrantWorkflowAccessAsync).RequireAuthorization();
        endpoints.MapPost("/admin/workflows/grants/revoke", RevokeWorkflowAccessAsync).RequireAuthorization();
        endpoints.MapPost("/admin/access/profiles", UpsertProfileAsync).RequireAuthorization();
        endpoints.MapPost("/admin/access/assignments", AssignProfileAsync).RequireAuthorization();
        endpoints.MapPost("/admin/access/assignments/remove", UnassignProfileAsync).RequireAuthorization();
        endpoints.MapPost("/admin/access/groups", MapGroupAsync).RequireAuthorization();
        endpoints.MapPost("/admin/access/groups/remove", UnmapGroupAsync).RequireAuthorization();
        endpoints.MapPost("/admin/access/accounts", CreateLocalAccountAsync).RequireAuthorization();
        endpoints.MapPost("/admin/settings/update", UpdatePresentationSettingsAsync).RequireAuthorization();
    }

    private static async Task<IResult> StartAsync(
        HttpContext context,
        string processId,
        int version,
        IAntiforgery antiforgery,
        IWorkflowPortalService portal)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var actor = RequiredIdentity(context);
        var card = (await portal.ListStartableProcessesAsync(actor, context.RequestAborted).ConfigureAwait(false))
            .SingleOrDefault(candidate => candidate.Id == processId && candidate.Version == version);
        if (card is null) return Results.NotFound();
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var assignments = new Dictionary<TechnicalId, IdentityReference>();
        try
        {
            foreach (var node in card.DesignatedAssignmentNodes)
            {
                var encoded = form[$"assignment.{node.Value}.identity"].ToString();
                var separator = encoded.IndexOf('|');
                if (separator <= 0 || separator == encoded.Length - 1)
                    throw new ArgumentException("A selected candidate identity is required.");
                assignments.Add(node, new(
                    Uri.UnescapeDataString(encoded[..separator]),
                    Uri.UnescapeDataString(encoded[(separator + 1)..])));
            }
            var request = new WorkflowStartRequest(
                processId,
                version,
                Serialize(card.StartForm, form),
                new(form["idempotencyKey"].ToString()),
                assignments);
            var result = await portal.StartAsync(actor, request, context.RequestAborted).ConfigureAwait(false);
            return result.Succeeded
                ? Results.Redirect($"/requests?started={result.InstanceId!.Value.Value:D}")
                : Results.Redirect($"/start/{Uri.EscapeDataString(processId)}/{version}?error={Uri.EscapeDataString(result.ErrorCode ?? "invalid")}");
        }
        catch (ArgumentException)
        {
            return Results.Redirect($"/start/{Uri.EscapeDataString(processId)}/{version}?error=invalid");
        }
    }

    private static async Task<IResult> CompleteTaskAsync(
        HttpContext context,
        Guid taskId,
        IAntiforgery antiforgery,
        IWorkflowPortalService portal)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var actor = RequiredIdentity(context);
        var identifier = new HumanTaskId(taskId);
        var task = await portal.ReadTaskAsync(actor, identifier, context.RequestAborted).ConfigureAwait(false);
        if (task is null) return Results.NotFound();
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        if (!long.TryParse(form["revision"], NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
            return Results.Redirect($"/tasks/{taskId:D}?error=revision");
        try
        {
            var request = new WorkflowTaskActionRequest(
                identifier,
                revision,
                new(form["action"].ToString()),
                new(form["idempotencyKey"].ToString()),
                Serialize(task.Form, form));
            var result = await portal.CompleteTaskAsync(actor, request, context.RequestAborted).ConfigureAwait(false);
            return result.Succeeded
                ? Results.Redirect($"/tasks/{taskId:D}?completed=1")
                : Results.Redirect($"/tasks/{taskId:D}?error={Uri.EscapeDataString(result.ErrorCode ?? "invalid")}");
        }
        catch (ArgumentException)
        {
            return Results.Redirect($"/tasks/{taskId:D}?error=invalid");
        }
    }

    private static Task<IResult> ClaimTaskAsync(
        HttpContext context, Guid taskId, IAntiforgery antiforgery, IWorkflowPortalService portal) =>
        ChangeTaskOwnershipAsync(context, taskId, antiforgery, portal, claim: true);

    private static Task<IResult> ReleaseTaskAsync(
        HttpContext context, Guid taskId, IAntiforgery antiforgery, IWorkflowPortalService portal) =>
        ChangeTaskOwnershipAsync(context, taskId, antiforgery, portal, claim: false);

    private static async Task<IResult> ChangeTaskOwnershipAsync(
        HttpContext context, Guid taskId, IAntiforgery antiforgery, IWorkflowPortalService portal, bool claim)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        if (!long.TryParse(form["revision"], NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
            return Results.Redirect($"/tasks/{taskId:D}?error=revision");
        var result = claim
            ? await portal.ClaimTaskAsync(RequiredIdentity(context), new(taskId), revision, context.RequestAborted).ConfigureAwait(false)
            : await portal.ReleaseTaskAsync(RequiredIdentity(context), new(taskId), revision, context.RequestAborted).ConfigureAwait(false);
        return result.Succeeded
            ? Results.Redirect($"/tasks/{taskId:D}")
            : Results.Redirect($"/tasks/{taskId:D}?error={Uri.EscapeDataString(result.ErrorCode ?? "conflict")}");
    }

    private static async Task<IResult> GrantWorkflowAccessAsync(
        HttpContext context, IAntiforgery antiforgery, WorkflowAccessAdministration administration)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var result = await administration.GrantAsync(RequiredIdentity(context), Scope(form), Recipient(form),
                context.RequestAborted).ConfigureAwait(false);
            return AdminRedirect("/admin/workflows", result.Outcome, result.ErrorCode);
        }
        catch (ArgumentException) { return AdminRedirect("/admin/workflows", ProviderOutcome.Conflict, "invalid"); }
    }

    private static async Task<IResult> RevokeWorkflowAccessAsync(
        HttpContext context, IAntiforgery antiforgery, WorkflowAccessAdministration administration)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var result = await administration.RevokeAsync(RequiredIdentity(context), Scope(form), Recipient(form),
                context.RequestAborted).ConfigureAwait(false);
            return AdminRedirect("/admin/workflows", result.Outcome, result.ErrorCode);
        }
        catch (ArgumentException) { return AdminRedirect("/admin/workflows", ProviderOutcome.Conflict, "invalid"); }
    }

    private static async Task<IResult> UpsertProfileAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        ISecurityProfileStore profiles)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var actor = RequiredIdentity(context);
        if (!await CanManageAsync(authorization, actor, context.RequestAborted).ConfigureAwait(false))
            return Results.Forbid();
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var permissions = form["permissions"].ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(value => new PermissionId(value)).ToHashSet();
            var profile = new SecurityProfile(form["profileId"].ToString(), form["displayName"].ToString(), permissions,
                IsTrue(form["administrative"].ToString()));
            var result = await profiles.UpsertProfileAsync(profile, actor, context.RequestAborted).ConfigureAwait(false);
            return AdminRedirect("/admin/access", result.Outcome, result.ErrorCode);
        }
        catch (ArgumentException) { return AdminRedirect("/admin/access", ProviderOutcome.Conflict, "invalid"); }
    }

    private static Task<IResult> AssignProfileAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        ISecurityProfileStore profiles) => ChangeProfileAssignmentAsync(context, antiforgery, authorization, profiles, true);

    private static Task<IResult> UnassignProfileAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        ISecurityProfileStore profiles) => ChangeProfileAssignmentAsync(context, antiforgery, authorization, profiles, false);

    private static async Task<IResult> ChangeProfileAssignmentAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        ISecurityProfileStore profiles, bool add)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var actor = RequiredIdentity(context);
        if (!await CanManageAsync(authorization, actor, context.RequestAborted).ConfigureAwait(false))
            return Results.Forbid();
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var identity = new IdentityReference(form["providerId"].ToString(), form["subjectId"].ToString());
            var profileId = form["profileId"].ToString();
            var result = add
                ? await profiles.AssignIdentityAsync(identity, profileId, actor, context.RequestAborted).ConfigureAwait(false)
                : await profiles.UnassignIdentityAsync(identity, profileId, actor, context.RequestAborted).ConfigureAwait(false);
            return AdminRedirect("/admin/access", result.Outcome, result.ErrorCode);
        }
        catch (ArgumentException) { return AdminRedirect("/admin/access", ProviderOutcome.Conflict, "invalid"); }
    }

    private static Task<IResult> MapGroupAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        ISecurityProfileStore profiles) => ChangeGroupMappingAsync(context, antiforgery, authorization, profiles, true);

    private static Task<IResult> UnmapGroupAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        ISecurityProfileStore profiles) => ChangeGroupMappingAsync(context, antiforgery, authorization, profiles, false);

    private static async Task<IResult> ChangeGroupMappingAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        ISecurityProfileStore profiles, bool add)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var actor = RequiredIdentity(context);
        if (!await CanManageAsync(authorization, actor, context.RequestAborted).ConfigureAwait(false))
            return Results.Forbid();
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var group = new GroupReference(form["providerId"].ToString(), form["groupId"].ToString());
            var profileId = form["profileId"].ToString();
            var result = add
                ? await profiles.MapGroupAsync(group, profileId, actor, context.RequestAborted).ConfigureAwait(false)
                : await profiles.UnmapGroupAsync(group, profileId, actor, context.RequestAborted).ConfigureAwait(false);
            return AdminRedirect("/admin/access", result.Outcome, result.ErrorCode);
        }
        catch (ArgumentException) { return AdminRedirect("/admin/access", ProviderOutcome.Conflict, "invalid"); }
    }

    private static async Task<IResult> CreateLocalAccountAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        LocalAccountAdministration accounts)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var actor = RequiredIdentity(context);
        if (!await CanManageAsync(authorization, actor, context.RequestAborted).ConfigureAwait(false))
            return Results.Forbid();
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var result = await accounts.CreateAsync(form["userName"].ToString(), form["password"].ToString(), actor,
                context.RequestAborted).ConfigureAwait(false);
            return AdminRedirect("/admin/access", result.Outcome, result.ErrorCode);
        }
        catch (ArgumentException) { return AdminRedirect("/admin/access", ProviderOutcome.Conflict, "invalid"); }
    }

    private static async Task<IResult> UpdatePresentationSettingsAsync(
        HttpContext context, IAntiforgery antiforgery, IAuthorizationProvider authorization,
        IPresentationSettingsStore settingsStore)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var actor = RequiredIdentity(context);
        if (!await CanManageAsync(authorization, actor, context.RequestAborted).ConfigureAwait(false))
            return Results.Forbid();
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        if (!long.TryParse(form["revision"], NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
            return AdminRedirect("/admin/settings", ProviderOutcome.Conflict, "invalid");
        var logo = form["logoResourcePath"].ToString().Trim();
        var settings = new PresentationSettings(
            form["displayName"].ToString().Trim(),
            string.IsNullOrWhiteSpace(logo) ? null : logo,
            form["accentColor"].ToString().Trim().ToUpperInvariant(),
            form["timeZoneId"].ToString().Trim(),
            form["culture"].ToString().Trim(),
            revision);
        var result = await settingsStore.UpdateAsync(settings, actor, context.RequestAborted).ConfigureAwait(false);
        return AdminRedirect("/admin/settings", result.Outcome, result.ErrorCode);
    }

    private static WorkflowAuthorizationScope Scope(IFormCollection form)
    {
        if (!int.TryParse(form["definitionVersion"], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            throw new ArgumentException("Definition version is invalid.");
        var node = form["nodeId"].ToString();
        var action = form["actionId"].ToString();
        var encoded = form["scope"].ToString();
        if (!string.IsNullOrWhiteSpace(encoded))
        {
            var separator = encoded.IndexOf('|');
            if (separator < 0) throw new ArgumentException("Workflow scope is invalid.");
            node = encoded[..separator] == "~" ? string.Empty : encoded[..separator];
            action = encoded[(separator + 1)..];
        }
        return new(form["workflowId"].ToString(), version, string.IsNullOrWhiteSpace(node) ? null : node,
            new(action));
    }

    private static WorkflowGrantRecipient Recipient(IFormCollection form) =>
        string.Equals(form["recipientKind"], "profile", StringComparison.Ordinal)
            ? WorkflowGrantRecipient.ForProfile(form["recipientProfileId"].ToString())
            : WorkflowGrantRecipient.ForIdentity(new(form["recipientProviderId"].ToString(),
                form["recipientSubjectId"].ToString()));

    private static async ValueTask<bool> CanManageAsync(
        IAuthorizationProvider authorization, IdentityReference actor, CancellationToken cancellationToken) =>
        (await authorization.AuthorizeAsync(new(actor, WorkflowPermissions.ManageAccess), cancellationToken)
            .ConfigureAwait(false)).Allowed;

    private static IResult AdminRedirect(string path, ProviderOutcome outcome, string? errorCode) =>
        outcome is ProviderOutcome.Succeeded
            ? Results.Redirect($"{path}?saved=1")
            : Results.Redirect($"{path}?error={Uri.EscapeDataString(errorCode ?? outcome.ToString())}");

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "on" || value == "1";

    internal static string Serialize(WorkflowFormDefinition definition, IFormCollection form)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var field in definition.Fields.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
            {
                var name = $"field.{field.Id.Value}";
                if (field.Kind is WorkflowFormFieldKind.Boolean)
                {
                    writer.WriteBoolean(field.Id.Value,
                        form.TryGetValue(name, out var booleanValues) &&
                        booleanValues.Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)));
                    continue;
                }
                if (!form.TryGetValue(name, out var values) || string.IsNullOrEmpty(values.ToString())) continue;
                var value = values.ToString();
                writer.WritePropertyName(field.Id.Value);
                if (field.Kind is WorkflowFormFieldKind.Number &&
                    decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    writer.WriteNumberValue(number);
                else
                    writer.WriteStringValue(value);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static IdentityReference RequiredIdentity(HttpContext context)
    {
        var provider = context.User.FindFirst(WorkflowSessionAuthenticationHandler.ProviderClaim)?.Value;
        var subject = context.User.FindFirst(WorkflowSessionAuthenticationHandler.SubjectClaim)?.Value;
        return provider is not null && subject is not null
            ? new(provider, subject)
            : throw new InvalidOperationException("An authenticated workflow identity is required.");
    }
}

using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnterpriseWorkflow.Web;

internal static class WorkflowWebReadEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workflow/media/{mediaId:guid}/{revision:int}/status", async (Guid mediaId, int revision, HttpContext context, WorkflowMediaPortalService media) =>
        {
            NoCache(context);
            var status = await media.InspectionStatusAsync(Actor(context), new(mediaId, revision), context.RequestAborted);
            return status is null ? Results.NotFound() : Results.Json(new { status });
        }).RequireAuthorization();
        endpoints.MapGet("/workflow/media/{mediaId:guid}/{revision:int}", async (Guid mediaId, int revision, HttpContext context, WorkflowMediaPortalService media) =>
        {
            NoCache(context);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            try
            {
                var result = await media.OpenAsync(Actor(context), new(mediaId, revision), context.RequestAborted);
                return result is { } file ? Results.File(file.Content, file.Metadata.ContentType, file.Metadata.Name) : Results.NotFound();
            }
            catch (FileNotFoundException) { return Results.NotFound(); }
        }).RequireAuthorization();
        endpoints.MapPost("/workflow/start/{processId}/{version:int}/media/{fieldId}", async (
            string processId, int version, string fieldId, HttpContext context, IAntiforgery antiforgery, WorkflowMediaPortalService media) =>
        {
            NoCache(context);
            try
            {
                SetUploadLimit(context);
                await antiforgery.ValidateRequestAsync(context);
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                if (form.Files.Count != 1) return Results.BadRequest();
                var file = form.Files[0];
                await using var stream = file.OpenReadStream();
                var reference = await media.UploadStartAsync(Actor(context), processId, version, fieldId, form["submissionKey"].ToString(), form["operationId"].ToString(), file.FileName, stream, context.RequestAborted);
                return reference is null ? Results.NotFound() : Results.Json(reference);
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = "media.upload-rejected" }); }
        }).RequireAuthorization();
        endpoints.MapPost("/workflow/tasks/{taskId:guid}/media/{fieldId}", async (
            Guid taskId, string fieldId, HttpContext context, IAntiforgery antiforgery, WorkflowMediaPortalService media) =>
        {
            NoCache(context);
            try
            {
                SetUploadLimit(context);
                await antiforgery.ValidateRequestAsync(context);
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                if (form.Files.Count != 1) return Results.BadRequest();
                var file = form.Files[0];
                await using var stream = file.OpenReadStream();
                var reference = await media.UploadTaskAsync(Actor(context), new(taskId), fieldId, form["operationId"].ToString(), file.FileName, stream, context.RequestAborted);
                return reference is null ? Results.NotFound() : Results.Json(reference);
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = "media.upload-rejected" }); }
        }).RequireAuthorization();
        endpoints.MapGet("/admin/tat/export", async (HttpContext context, WorkflowTatService tat, IPresentationSettingsStore settings) =>
        {
            NoCache(context);
            try
            {
                var q = context.Request.Query;
                var zone = (await settings.ReadAsync(context.RequestAborted)).Value?.TimeZoneId ?? "UTC";
                var dates = WorkflowAuditFilters.Create(q["from"], q["until"], q["provider"], q["analyst"], null, null, null, null, null, zone);
                var version = string.IsNullOrEmpty(q["version"]) ? (int?)null : int.Parse(q["version"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
                var query = new WorkflowTatQuery(q["process"], version, q["node"], dates.Actor is { } actor ? new(new(actor.ProviderId), actor.SubjectId) : null,
                    q["group"] == "analyst", dates.FromUtc, dates.UntilUtc);
                var result = await tat.ExportAsync(Actor(context), query, context.RequestAborted);
                return result.Content is { } bytes ? Results.File(bytes, "text/csv; charset=utf-8", "tat.csv") : Results.BadRequest(new { error = result.ErrorCode });
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException) { return Results.BadRequest(); }
        }).RequireAuthorization();
        endpoints.MapGet("/admin/audit/export", async (HttpContext context, WorkflowAuditService audit, IPresentationSettingsStore settings) =>
        {
            NoCache(context);
            try
            {
                var q = context.Request.Query;
                var zone = (await settings.ReadAsync(context.RequestAborted)).Value?.TimeZoneId ?? "UTC";
                var query = WorkflowAuditFilters.Create(q["from"], q["until"], q["provider"], q["actor"], q["action"], q["target"], q["outcome"], null, null, zone, q["module"], q["process"], q["stage"]);
                var result = await audit.ExportAsync(Actor(context), query, context.RequestAborted);
                return result.Content is { } bytes ? Results.File(bytes, "text/csv; charset=utf-8", "audit.csv") :
                    result.ErrorCode == "security.audit-export-denied" ? Results.Forbid() : Results.BadRequest(new { error = result.ErrorCode });
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { error = "security.audit-query-invalid" }); }
        }).RequireAuthorization();
        endpoints.MapGet("/workflow/candidates/start/{processId}/{version:int}/{nodeId}", async (
            string processId, int version, string nodeId, string q, HttpContext context, IWorkflowPortalService portal) =>
        {
            NoCache(context);
            if (!Valid(q) || !TechnicalId.IsValid(nodeId)) return Results.BadRequest();
            return await Candidates(() => portal.SearchAssignmentCandidatesAsync(Actor(context), processId, version,
                new(nodeId), q, 20, context.RequestAborted));
        }).RequireAuthorization();
        endpoints.MapGet("/workflow/candidates/task/{taskId:guid}/{action}", async (
            Guid taskId, string action, string q, HttpContext context, IWorkflowPortalService portal) =>
        {
            NoCache(context);
            if (!Valid(q) || !TechnicalId.IsValid(action)) return Results.BadRequest();
            return await Candidates(() => portal.SearchTaskAssignmentCandidatesAsync(Actor(context), new(taskId),
                new(action), q, 20, context.RequestAborted));
        }).RequireAuthorization();
        endpoints.MapGet("/workflow/candidates/recovery/{taskId:guid}", async (
            Guid taskId, string q, HttpContext context, IWorkflowPortalService portal) =>
        {
            NoCache(context);
            if (!Valid(q)) return Results.BadRequest();
            return await Candidates(() => portal.SearchRecoveryCandidatesAsync(Actor(context), new(taskId), q, 20, context.RequestAborted));
        }).RequireAuthorization();
    }

    private static async Task<IResult> Candidates(Func<ValueTask<IReadOnlyList<WorkflowCandidate>>> search)
    {
        try { return Results.Json(await search()); }
        catch (InvalidOperationException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }
    private static void SetUploadLimit(HttpContext context)
    {
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature) feature.MaxRequestBodySize = 101 * 1024 * 1024;
    }
    private static bool Valid(string q) => q.Trim().Length >= 2 && q.Length <= 256;
    internal static IdentityReference Actor(HttpContext context) => new(
        context.User.FindFirst(WorkflowSessionAuthenticationHandler.ProviderClaim)!.Value,
        context.User.FindFirst(WorkflowSessionAuthenticationHandler.SubjectClaim)!.Value);
    private static void NoCache(HttpContext context) => context.Response.Headers.CacheControl = "no-store, private";
}

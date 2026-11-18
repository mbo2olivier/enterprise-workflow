using EnterpriseWorkflow.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Web;

internal static class WorkflowIdentityProvisioningEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/admin/identities/{providerId}/submit", async (string providerId, HttpContext context,
            IAntiforgery antiforgery, IdentityProvisioningAdministration administration) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false)) return Results.BadRequest();
            var provider = Find(context, providerId);
            if (provider is null) return Results.NotFound();
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var result = await administration.SubmitAsync(provider, new(form["identifier"].ToString(),
                form["profiles"].ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), Actor(context)), context.RequestAborted).ConfigureAwait(false);
            return Result(result);
        }).RequireAuthorization();
        endpoints.MapPost("/admin/identities/{providerId}/decide", async (string providerId, HttpContext context,
            IAntiforgery antiforgery, IdentityProvisioningAdministration administration) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false)) return Results.BadRequest();
            var provider = Find(context, providerId);
            if (provider is null) return Results.NotFound();
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            if (!long.TryParse(form["revision"], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var revision) ||
                form["decision"].ToString() is not ("approve" or "reject")) return Results.BadRequest();
            var result = await administration.DecideAsync(provider, new(form["requestId"].ToString(), revision, Actor(context), form["reason"].ToString()),
                form["decision"] == "approve", context.RequestAborted).ConfigureAwait(false);
            return Result(result);
        }).RequireAuthorization();
    }
    private static IIdentityProvisioningProvider? Find(HttpContext context, string id) =>
        context.RequestServices.GetServices<IIdentityProvisioningProvider>().SingleOrDefault(provider => provider.ProviderId == id);
    private static IdentityReference Actor(HttpContext context) => new(
        context.User.FindFirst(WorkflowSessionAuthenticationHandler.ProviderClaim)!.Value,
        context.User.FindFirst(WorkflowSessionAuthenticationHandler.SubjectClaim)!.Value);
    private static IResult Result(ProviderResult<IdentityProvisioningRequest> result) => result.Outcome switch
    {
        ProviderOutcome.Succeeded => Results.Redirect("/admin/identity-provisioning"),
        ProviderOutcome.Forbidden => Results.Forbid(),
        ProviderOutcome.Conflict => Results.Conflict(new { result.ErrorCode }),
        ProviderOutcome.NotFound => Results.NotFound(),
        _ => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
    };
}

using System.Globalization;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Web.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EnterpriseWorkflow.Web;

public static class EnterpriseWorkflowWebExtensions
{
    public static IServiceCollection AddEnterpriseWorkflowWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IdentityProvisioningAdministration>();
        services.AddEnterpriseWorkflowLocalization();
        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<WorkflowBrandingOptions>();
        services.TryAddSingleton<SecondFactorOptions>();
        services.AddSingleton<WorkflowSecondFactorFlow>();
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();
        services.AddSingleton<WorkflowFormFeedbackStore>();
        services.AddScoped<WorkflowWebIdentityAccessor>();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = WorkflowSessionAuthenticationHandler.SchemeName;
            options.DefaultChallengeScheme = WorkflowSessionAuthenticationHandler.SchemeName;
        }).AddScheme<AuthenticationSchemeOptions, WorkflowSessionAuthenticationHandler>(
            WorkflowSessionAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();
        services.AddAntiforgery();
        return services;
    }

    public static IEndpointRouteBuilder MapEnterpriseWorkflowWeb(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints is IApplicationBuilder application) application.UseEnterpriseWorkflowLocalization();
        endpoints.MapPost("/preferences/language", async (HttpContext context, IAntiforgery antiforgery, WorkflowLocalizationOptions localization) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false)) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            var culture = form["culture"].ToString();
            if (culture.Length != 0 && !localization.SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest();
            var store = context.RequestServices.GetService<IUserCulturePreferenceStore>();
            if (store is null) return Results.StatusCode(StatusCodes.Status501NotImplemented);
            var provider = context.User.FindFirst(WorkflowSessionAuthenticationHandler.ProviderClaim)!.Value;
            var subject = context.User.FindFirst(WorkflowSessionAuthenticationHandler.SubjectClaim)!.Value;
            await store.SetCultureAsync(new(provider, subject), culture.Length == 0 ? null : CultureInfo.GetCultureInfo(culture).Name, context.RequestAborted).ConfigureAwait(false);
            return Results.LocalRedirect(SafeReturnUrl(form["returnUrl"].ToString()));
        }).RequireAuthorization();
        endpoints.MapPost("/auth/login", LoginAsync).AllowAnonymous();
        var options = endpoints.ServiceProvider.GetRequiredService<SecondFactorOptions>();
        if (options.Enabled && endpoints.ServiceProvider.GetService<ISecondFactorHandler>() is null)
            throw new InvalidOperationException("Second authentication is enabled but no compiled handler is registered.");
        endpoints.MapPost("/auth/second-factor", VerifySecondFactorAsync).AllowAnonymous();
        endpoints.MapPost("/auth/second-factor/cancel", CancelSecondFactorAsync).AllowAnonymous();
        endpoints.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();
        WorkflowIdentityProvisioningEndpoints.Map(endpoints);
        WorkflowWebMutationEndpoints.Map(endpoints);
        WorkflowWebReadEndpoints.Map(endpoints);
        endpoints.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationProvider authentication,
        SecuritySessionManager sessions)
    {
        if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false)) return Results.BadRequest();
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var userName = form["userName"].ToString();
        var password = form["password"].ToString();
        var returnUrl = SafeReturnUrl(form["returnUrl"].ToString());
        var authenticated = await authentication.AuthenticateAsync(new(userName, password), context.RequestAborted)
            .ConfigureAwait(false);
        if (authenticated.Status is not AuthenticationStatus.Succeeded || authenticated.Identity is null)
            return Results.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
        var admission = await sessions.CanIssueAsync(authenticated.Identity.Value, context.RequestAborted).ConfigureAwait(false);
        if (!admission) return Results.Redirect("/login?error=invalid");
        if (context.RequestServices.GetRequiredService<SecondFactorOptions>().Enabled)
        {
            var flow = context.RequestServices.GetRequiredService<WorkflowSecondFactorFlow>();
            return await flow.BeginAsync(context, authenticated.Identity.Value, returnUrl).ConfigureAwait(false)
                ? Results.Redirect("/second-factor") : Results.Redirect("/login?error=unavailable");
        }
        return await IssueSessionAsync(context, sessions, authenticated.Identity.Value, returnUrl).ConfigureAwait(false);
    }

    private static async Task<IResult> IssueSessionAsync(HttpContext context, SecuritySessionManager sessions, IdentityReference identity, string returnUrl, bool secondFactorVerified = false)
    {
        var issued = await sessions.IssueAsync(identity, secondFactorVerified, context.RequestAborted).ConfigureAwait(false);
        if (issued.Outcome is not ProviderOutcome.Succeeded || issued.Value is null)
            return Results.Redirect($"/login?error=unavailable&returnUrl={Uri.EscapeDataString(returnUrl)}");
        context.Response.Cookies.Append(WorkflowSessionAuthenticationHandler.CookieName, issued.Value.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = issued.Value.ExpiresAtUtc,
                Path = "/",
            });
        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> VerifySecondFactorAsync(HttpContext context, IAntiforgery antiforgery,
        WorkflowSecondFactorFlow flow, SecuritySessionManager sessions)
    {
        if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false)) return Results.BadRequest();
        var pending = flow.Read(context);
        if (pending is null) return Results.Redirect("/login?error=expired");
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var proof = form["proof"].ToString();
        if (proof.Length > 1024) return Results.BadRequest();
        var handler = context.RequestServices.GetRequiredService<ISecondFactorHandler>();
        var result = await handler.VerifyAsync(new(pending.Identity, pending.AttemptId, pending.ChallengeId, proof), context.RequestAborted).ConfigureAwait(false);
        if (result.Status is SecondFactorStatus.Succeeded)
        {
            WorkflowSecondFactorFlow.Clear(context);
            return await IssueSessionAsync(context, sessions, pending.Identity, SafeReturnUrl(pending.ReturnUrl), secondFactorVerified: true).ConfigureAwait(false);
        }
        if (result.Status is SecondFactorStatus.Expired) WorkflowSecondFactorFlow.Clear(context);
        return Results.Redirect("/second-factor?error=verification");
    }

    private static async Task<IResult> CancelSecondFactorAsync(HttpContext context, IAntiforgery antiforgery, WorkflowSecondFactorFlow flow)
    {
        if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false)) return Results.BadRequest();
        if (flow.Read(context) is { } pending)
            await context.RequestServices.GetRequiredService<ISecondFactorHandler>()
                .CancelAsync(new(pending.Identity, pending.AttemptId), pending.ChallengeId, context.RequestAborted).ConfigureAwait(false);
        WorkflowSecondFactorFlow.Clear(context);
        return Results.Redirect("/login");
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        SecuritySessionManager sessions)
    {
        if (!await antiforgery.IsRequestValidAsync(context).ConfigureAwait(false)) return Results.BadRequest();
        if (context.Request.Cookies.TryGetValue(WorkflowSessionAuthenticationHandler.CookieName, out var token))
        {
            var providerId = context.User.FindFirst(WorkflowSessionAuthenticationHandler.ProviderClaim)?.Value;
            var subjectId = context.User.FindFirst(WorkflowSessionAuthenticationHandler.SubjectClaim)?.Value;
            if (providerId is not null && subjectId is not null)
                await sessions.RevokeAsync(token, new(providerId, subjectId), context.RequestAborted).ConfigureAwait(false);
        }
        context.Response.Cookies.Delete(WorkflowSessionAuthenticationHandler.CookieName, new CookieOptions { Path = "/" });
        return Results.Redirect("/login");
    }

    internal static string SafeReturnUrl(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') &&
        !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') && !value.Any(char.IsControl)
            ? value
            : "/";
}

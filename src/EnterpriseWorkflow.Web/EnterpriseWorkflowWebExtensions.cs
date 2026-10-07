using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Web.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Web;

public static class EnterpriseWorkflowWebExtensions
{
    public static IServiceCollection AddEnterpriseWorkflowWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();
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
        endpoints.MapPost("/auth/login", LoginAsync).AllowAnonymous();
        endpoints.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();
        WorkflowWebMutationEndpoints.Map(endpoints);
        endpoints.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationProvider authentication,
        SecuritySessionManager sessions)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var userName = form["userName"].ToString();
        var password = form["password"].ToString();
        var returnUrl = SafeReturnUrl(form["returnUrl"].ToString());
        var authenticated = await authentication.AuthenticateAsync(new(userName, password), context.RequestAborted)
            .ConfigureAwait(false);
        if (authenticated.Status is not AuthenticationStatus.Succeeded || authenticated.Identity is null)
            return Results.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
        var issued = await sessions.IssueAsync(authenticated.Identity.Value, context.RequestAborted).ConfigureAwait(false);
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

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        SecuritySessionManager sessions)
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
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

    private static string SafeReturnUrl(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') &&
        !value.StartsWith("//", StringComparison.Ordinal)
            ? value
            : "/";
}

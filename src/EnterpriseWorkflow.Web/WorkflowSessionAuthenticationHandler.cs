using System.Security.Claims;
using System.Text.Encodings.Web;
using EnterpriseWorkflow.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Web;

public sealed class WorkflowSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SecuritySessionManager sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "EnterpriseWorkflow.Session";
    public const string CookieName = "ew_session";
    public const string ProviderClaim = "ew:provider";
    public const string SubjectClaim = "ew:subject";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrWhiteSpace(token))
            return AuthenticateResult.NoResult();
        var validation = await sessions.ValidateAsync(token, Context.RequestAborted).ConfigureAwait(false);
        if (validation.Status is not SessionValidationStatus.Valid || validation.Identity is null)
            return AuthenticateResult.Fail(validation.ErrorCode ?? "security.session-invalid");
        var identity = validation.Identity.Value;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"{identity.ProviderId}:{identity.SubjectId}"),
            new Claim(ProviderClaim, identity.ProviderId),
            new Claim(SubjectClaim, identity.SubjectId),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
        Response.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return Task.CompletedTask;
    }
}

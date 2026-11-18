using System.Security.Cryptography;
using System.Text.Json;
using EnterpriseWorkflow.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Web;

/// <summary>Browser-bound pending authentication; this cookie is never a session credential.</summary>
public sealed class WorkflowSecondFactorFlow(IDataProtectionProvider protection, SecondFactorOptions options, TimeProvider clock)
{
    private const string CookieName = "ew.second-factor";
    private readonly IDataProtector _protector = protection.CreateProtector("EnterpriseWorkflow.SecondFactor.v1");
    public sealed record PendingAuthentication(IdentityReference Identity, string AttemptId, string ChallengeId,
        DateTimeOffset ExpiresAtUtc, string ReturnUrl, string? Prompt);

    public async ValueTask<bool> BeginAsync(HttpContext context, IdentityReference identity, string returnUrl)
    {
        if (!options.Enabled) return false;
        var handler = context.RequestServices.GetRequiredService<ISecondFactorHandler>();
        var attempt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var result = await handler.BeginAsync(new(identity, attempt), context.RequestAborted).ConfigureAwait(false);
        if (result.Outcome is not ProviderOutcome.Succeeded || result.Value is not { } challenge) return false;
        var now = clock.GetUtcNow();
        if (string.IsNullOrWhiteSpace(challenge.ChallengeId) || challenge.ChallengeId.Length > 256 ||
            challenge.ExpiresAtUtc <= now || challenge.ExpiresAtUtc > now.AddMinutes(10) || challenge.Prompt?.Length > 512)
        {
            await handler.CancelAsync(new(identity, attempt), challenge.ChallengeId, context.RequestAborted).ConfigureAwait(false);
            return false;
        }
        var pending = new PendingAuthentication(identity, attempt, challenge.ChallengeId, challenge.ExpiresAtUtc, returnUrl, challenge.Prompt);
        context.Response.Cookies.Append(CookieName, _protector.Protect(JsonSerializer.Serialize(pending)), new CookieOptions
        {
            HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict,
            IsEssential = true, Path = "/", Expires = challenge.ExpiresAtUtc,
        });
        return true;
    }

    public PendingAuthentication? Read(HttpContext context)
    {
        if (!options.Enabled || !context.Request.Cookies.TryGetValue(CookieName, out var value) || value.Length > 8192) return null;
        try
        {
            var pending = JsonSerializer.Deserialize<PendingAuthentication>(_protector.Unprotect(value));
            return pending is not null && pending.ExpiresAtUtc > clock.GetUtcNow() ? pending : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException) { return null; }
    }

    public static void Clear(HttpContext context) => context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
}

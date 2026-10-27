using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Web;
using Microsoft.AspNetCore.Antiforgery;

internal static class DevelopmentSeed
{
    internal static async Task ApplyAsync(IServiceProvider services, DevelopmentConfiguration config, string stateRoot)
    {
        var profiles = services.GetRequiredService<ISecurityProfileStore>();
        var accounts = services.GetRequiredService<ILocalAccountStore>();
        var authentication = services.GetRequiredService<LocalAuthenticationProvider>();
        var permissions = services.GetRequiredService<IWorkflowPresentationCatalog>().Permissions.Select(item => item.Id.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var user in config.Seed.Users)
            foreach (var permission in user.Permissions)
                if (!permissions.Contains(permission)) throw new ArgumentException($"Unknown seed permission '{permission}' for '{user.Id}'.");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config.Seed))));
        var seedPath = Path.Combine(stateRoot, "seed.sha256");
        if (File.Exists(seedPath))
        {
            if (await File.ReadAllTextAsync(seedPath) != fingerprint)
                throw new InvalidOperationException("Seed changed. Restore the original configuration or explicitly reset the local environment; existing permissions were not overwritten.");
            return;
        }
        var pending = Path.Combine(stateRoot, "seed.pending");
        if (File.Exists(pending)) throw new InvalidOperationException("Interrupted seed initialization. Explicitly reset the development environment before retrying.");
        await File.WriteAllTextAsync(pending, fingerprint);
        var adminResult = await accounts.FindByNormalizedNameAsync("EW-ADMIN", CancellationToken.None);
        if (adminResult.Value is null)
        {
            var provisioner = new LocalSecurityProvisioner(services.GetRequiredService<ILocalSecurityProvisioningStore>(), authentication);
            Ensure(await provisioner.BootstrapAsync("ew-admin", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), CancellationToken.None));
            adminResult = await accounts.FindByNormalizedNameAsync("EW-ADMIN", CancellationToken.None);
        }
        var admin = adminResult.Value?.Identity ?? throw new InvalidOperationException("Development administrator unavailable.");
        foreach (var user in config.Seed.Users)
        {
            var identity = new IdentityReference(SecurityProviderIds.Local, "ew-dev-" + user.Id);
            var found = await accounts.FindByIdentityAsync(identity, CancellationToken.None);
            if (found.Value is null)
            {
                var account = new LocalAccount(identity, "ew-dev-" + user.Id, ("ew-dev-" + user.Id).ToUpperInvariant(), "", true, 0, null, 0);
                account = account with { PasswordHash = authentication.HashPassword(account, Convert.ToHexString(RandomNumberGenerator.GetBytes(32))) };
                Ensure(await accounts.CreateAsync(account, admin, CancellationToken.None));
            }
            var profile = new SecurityProfile("ew-dev-" + user.Id, user.DisplayName,
                user.Permissions.Select(value => new PermissionId(value)).ToHashSet(), false);
            Ensure(await profiles.UpsertProfileAsync(profile, admin, CancellationToken.None));
            Ensure(await profiles.AssignIdentityAsync(identity, profile.Id, admin, CancellationToken.None));
        }
        await File.WriteAllTextAsync(seedPath, fingerprint);
        File.Delete(pending);
    }

    internal static void MapSelector(WebApplication app, DevelopmentConfiguration config, WorkflowModuleCatalog catalog)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/login") { context.Response.Redirect("/__ew/dev"); return; }
            await next(context);
        });
        app.MapGet("/__ew/dev", async (HttpContext context, IAntiforgery antiforgery, ILocalAccountStore accounts) =>
        {
            context.Response.Headers.CacheControl = "no-store, private";
            var token = antiforgery.GetAndStoreTokens(context);
            var admin = (await accounts.FindByNormalizedNameAsync("EW-ADMIN", context.RequestAborted)).Value;
            var choices = config.Seed.Users.Select(user => (Id: "ew-dev-" + user.Id, Label: user.DisplayName)).ToList();
            if (admin is not null) choices.Add((admin.Identity.SubjectId, "Administration locale"));
            var options = string.Join("", choices.Select(choice => $"<option value=\"{WebUtility.HtmlEncode(choice.Id)}\">{WebUtility.HtmlEncode(choice.Label)}</option>"));
            var modules = string.Join(", ", catalog.Modules.Select(module => $"{module.Manifest.Id.Value} / {module.Manifest.Version} / {module.ArtifactSha256[..12]}"));
            return Results.Content($"""
                <!doctype html>
                <html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
                <title>ew dev · Enterprise Workflow</title><link rel="stylesheet" href="/_content/EnterpriseWorkflow.Web/enterprise-workflow.css"></head>
                <body><main class="login-page"><div class="grid-reveal login-grid-reveal" aria-hidden="true"></div>
                <section class="login-panel panel"><span class="brand-mark login-brand" aria-hidden="true">✦</span>
                <p class="eyebrow">Enterprise Workflow · Développement</p><h1>Explorer votre module</h1>
                <p>Choisissez une identité pour tester le parcours et le rendu de votre module.</p>
                <p class="dev-module-summary">{WebUtility.HtmlEncode(modules)}</p>
                <form method="post" action="/__ew/dev/identity" class="request-form login-form">
                <input type="hidden" name="{token.FormFieldName}" value="{WebUtility.HtmlEncode(token.RequestToken)}">
                <label>Identité<select name="identity">{options}</select></label>
                <button class="primary-button" type="submit">Explorer le module</button></form>
                <a class="dev-return-link" href="/">Revenir au portail</a></section></main>
                <script src="/_content/EnterpriseWorkflow.Web/enterprise-workflow.js" defer></script></body></html>
                """, "text/html; charset=utf-8");
        }).AllowAnonymous();
        app.MapPost("/__ew/dev/identity", async (HttpContext context, IAntiforgery antiforgery, SecuritySessionManager sessions, ILocalAccountStore accounts) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var selected = (await context.Request.ReadFormAsync())["identity"].ToString();
            var admin = (await accounts.FindByNormalizedNameAsync("EW-ADMIN", context.RequestAborted)).Value;
            if (!config.Seed.Users.Any(user => "ew-dev-" + user.Id == selected) && admin?.Identity.SubjectId != selected)
                return Results.BadRequest("Unknown development identity.");
            var identity = new IdentityReference(SecurityProviderIds.Local, selected);
            var account = (await accounts.FindByIdentityAsync(identity, context.RequestAborted)).Value;
            if (account is null || !account.Enabled) return Results.BadRequest("Identity disabled.");
            if (context.Request.Cookies.TryGetValue(WorkflowSessionAuthenticationHandler.CookieName, out var old))
                await sessions.RevokeAsync(old, identity, context.RequestAborted);
            var issued = await sessions.IssueAsync(identity, context.RequestAborted);
            if (issued.Value is null) return Results.Problem("Session unavailable.");
            context.Response.Cookies.Append(WorkflowSessionAuthenticationHandler.CookieName, issued.Value.Token,
                new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = issued.Value.ExpiresAtUtc });
            return Results.Redirect("/");
        }).AllowAnonymous();
    }

    private static void Ensure<T>(ProviderResult<T> result)
    {
        if (result.Outcome != ProviderOutcome.Succeeded) throw new InvalidOperationException(result.ErrorCode ?? "Development provisioning failed.");
    }
}

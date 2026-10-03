using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Security") ??
    new SqliteConnectionStringBuilder { DataSource = Path.Combine(AppContext.BaseDirectory, "security.db") }.ToString();
var database = new SqliteSecurityDatabase(connectionString);
await database.MigrateAsync();
var store = database.CreateStore();
var policy = builder.Configuration.GetSection("Security:Policy").Get<SecurityPolicyOptions>() ?? new();
policy.Validate();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = policy.MaximumRequestBodyBytes);
builder.Services.AddRateLimiter(options => options.AddPolicy("login", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = policy.LoginRateLimitPermitCount,
        Window = policy.LoginRateLimitWindow,
        QueueLimit = 0,
    })));
var local = new LocalAuthenticationProvider(store, policy, TimeProvider.System);
var sessions = new SecuritySessionManager(store, policy, new Dictionary<string, IIdentityDirectory>(), TimeProvider.System);
var authorization = new InternalProfileAuthorizationProvider(store, new Dictionary<string, IIdentityDirectory>());
var provisioning = new LocalSecurityProvisioner(store, local);
var accountAdministration = new LocalAccountAdministration(store, store, local);

if (args is ["bootstrap", var userName, var password])
{
    var result = await provisioning.BootstrapAsync(userName, password, CancellationToken.None);
    Console.WriteLine(result.Outcome is ProviderOutcome.Succeeded ? "Administrateur local initialisé." : $"Échec: {result.ErrorCode}");
    return result.Outcome is ProviderOutcome.Succeeded ? 0 : 2;
}
if (args is ["recover", var providerId, var subjectId])
{
    var result = await provisioning.RecoverAsync(new(providerId, subjectId), CancellationToken.None);
    Console.WriteLine(result.Outcome is ProviderOutcome.Succeeded ? "Accès administratif rétabli." : $"Échec: {result.ErrorCode}");
    return result.Outcome is ProviderOutcome.Succeeded ? 0 : 2;
}

var app = builder.Build();
app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapPost("/session", async (LoginRequest request, CancellationToken ct) =>
{
    var authenticated = await local.AuthenticateAsync(new(request.UserName, request.Password), ct);
    if (authenticated.Status is not AuthenticationStatus.Succeeded) return Results.Json(new { error = "invalid-credentials" }, statusCode: 401);
    var issued = await sessions.IssueAsync(authenticated.Identity!.Value, ct);
    return issued.Outcome is ProviderOutcome.Succeeded ? Results.Ok(issued.Value) : Results.StatusCode(503);
}).RequireRateLimiting("login");

app.MapDelete("/session", async (HttpRequest request, CancellationToken ct) =>
{
    var authenticated = await Authenticate(request, sessions, ct); if (authenticated is null) return Results.Unauthorized();
    var token = Bearer(request)!; var revoked = await sessions.RevokeAsync(token, authenticated.Value, ct);
    return revoked.Outcome is ProviderOutcome.Succeeded ? Results.NoContent() : Results.StatusCode(503);
});

app.MapPut("/admin/profiles/{profileId}", async (string profileId, ProfileRequest request, HttpRequest http, CancellationToken ct) =>
{
    var actor = await Require(http, sessions, authorization, WorkflowPermissions.ManageAccess, ct); if (actor is null) return Results.StatusCode(403);
    var profile = new SecurityProfile(profileId, request.DisplayName, request.Permissions.Select(x => new PermissionId(x)).ToHashSet(), request.GrantsAdministrativeAccess);
    var result = await store.UpsertProfileAsync(profile, actor.Value, ct); return result.Outcome is ProviderOutcome.Succeeded ? Results.Ok(result.Value) : Results.Conflict(new { error = result.ErrorCode });
});

app.MapPut("/admin/profiles/{profileId}/identities/{providerId}/{subjectId}", async (string profileId, string providerId, string subjectId, HttpRequest http, CancellationToken ct) =>
{
    var actor = await Require(http, sessions, authorization, WorkflowPermissions.ManageAccess, ct); if (actor is null) return Results.StatusCode(403);
    var result = await store.AssignIdentityAsync(new(providerId, subjectId), profileId, actor.Value, ct); return result.Outcome is ProviderOutcome.Succeeded ? Results.NoContent() : Results.Conflict(new { error = result.ErrorCode });
});

app.MapPut("/admin/profiles/{profileId}/groups/{providerId}/{groupId}", async (string profileId, string providerId, string groupId, HttpRequest http, CancellationToken ct) =>
{
    var actor = await Require(http, sessions, authorization, WorkflowPermissions.ManageAccess, ct); if (actor is null) return Results.StatusCode(403);
    var result = await store.MapGroupAsync(new(providerId, groupId), profileId, actor.Value, ct); return result.Outcome is ProviderOutcome.Succeeded ? Results.NoContent() : Results.Conflict(new { error = result.ErrorCode });
});

app.MapPost("/admin/local-accounts", async (CreateAccountRequest request, HttpRequest http, CancellationToken ct) =>
{
    var actor = await Require(http, sessions, authorization, WorkflowPermissions.ManageAccess, ct); if (actor is null) return Results.StatusCode(403);
    var result = await accountAdministration.CreateAsync(request.UserName, request.Password, actor.Value, ct); return result.Outcome is ProviderOutcome.Succeeded ? Results.Created($"/admin/local-accounts/{result.Value!.Identity.SubjectId}", new { result.Value.Identity, result.Value.UserName }) : Results.Conflict(new { error = result.ErrorCode });
});

app.MapPut("/admin/local-accounts/{subjectId}/enabled", async (string subjectId, AccountStatusRequest request, HttpRequest http, CancellationToken ct) =>
{
    var actor = await Require(http, sessions, authorization, WorkflowPermissions.ManageAccess, ct); if (actor is null) return Results.StatusCode(403);
    var result = await accountAdministration.SetEnabledAsync(new(SecurityProviderIds.Local, subjectId), request.Enabled, actor.Value, ct); return result.Outcome is ProviderOutcome.Succeeded ? Results.NoContent() : Results.Conflict(new { error = result.ErrorCode });
});

app.MapPut("/admin/local-accounts/{subjectId}/password", async (string subjectId, PasswordResetRequest request, HttpRequest http, CancellationToken ct) =>
{
    var actor = await Require(http, sessions, authorization, WorkflowPermissions.ManageAccess, ct); if (actor is null) return Results.StatusCode(403);
    var result = await accountAdministration.ResetPasswordAsync(new(SecurityProviderIds.Local, subjectId), request.Password, actor.Value, ct); return result.Outcome is ProviderOutcome.Succeeded ? Results.NoContent() : Results.Conflict(new { error = result.ErrorCode });
});

app.MapGet("/admin/audit", async (HttpRequest http, CancellationToken ct) =>
{
    var actor = await Require(http, sessions, authorization, WorkflowPermissions.ReadAudit, ct); return actor is null ? Results.StatusCode(403) : Results.Ok(await store.ReadAuditAsync(100, ct));
});

await app.RunAsync(); return 0;

static string? Bearer(HttpRequest request) { var value = request.Headers.Authorization.ToString(); return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : null; }
static async ValueTask<IdentityReference?> Authenticate(HttpRequest request, SecuritySessionManager sessions, CancellationToken ct) { var token = Bearer(request); if (token is null) return null; var result = await sessions.ValidateAsync(token, ct); return result.Status is SessionValidationStatus.Valid ? result.Identity : null; }
static async ValueTask<IdentityReference?> Require(HttpRequest request, SecuritySessionManager sessions, IAuthorizationProvider authorization, PermissionId permission, CancellationToken ct) { var identity = await Authenticate(request, sessions, ct); if (identity is null) return null; var decision = await authorization.AuthorizeAsync(new(identity.Value, permission), ct); return decision.Allowed ? identity : null; }

internal sealed record LoginRequest(string UserName, string Password);
internal sealed record ProfileRequest(string DisplayName, string[] Permissions, bool GrantsAdministrativeAccess);
internal sealed record CreateAccountRequest(string UserName, string Password);
internal sealed record AccountStatusRequest(bool Enabled);
internal sealed record PasswordResetRequest(string Password);

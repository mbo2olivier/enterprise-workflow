using System.Net;
using EnterpriseWorkflow.Hosting;
using EnterpriseWorkflow.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class SecondFactorTests
{
    [Fact]
    public async Task PrimaryLoginCannotIssueASessionAndOnlyOneSecondProofSucceeds()
    {
        var root = Path.Combine(Path.GetTempPath(), "ew-second-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var builder = EnterpriseWorkflowApplication.CreateWebBuilder([]).AddWebPresentation();
            builder.Environment.ApplicationName = typeof(EnterpriseWorkflowWebExtensions).Assembly.GetName().Name!;
            builder.Configuration["ConnectionStrings:EnterpriseWorkflow"] = $"Data Source={Path.Combine(root, "test.db")};Pooling=False";
            builder.Configuration["Media:Root"] = Path.Combine(root, "media");
            builder.Configuration["Security:SecondFactor:Enabled"] = "true";
            builder.Configuration["urls"] = "http://127.0.0.1:0";
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.AddSecondFactorHandler<SingleUseHandler>();
            await builder.MigrateAsync();
            Assert.Equal(ProviderOutcome.Succeeded, (await builder.BootstrapAsync("admin", "valid-password-123")).Outcome);
            await using var host = await builder.BuildAsync();
            host.MapEnterpriseWorkflow();
            var app = host.WebApplication;
            app.MapGet("/test/token", (HttpContext context, IAntiforgery antiforgery) => antiforgery.GetAndStoreTokens(context).RequestToken!);
            await app.StartAsync(TestContext.Current.CancellationToken);
            try
            {
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                using var transport = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
                using var client = new HttpClient(transport) { BaseAddress = new Uri(address) };
                var token = await client.GetStringAsync("/test/token", TestContext.Current.CancellationToken);
                using var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["userName"] = "admin", ["password"] = "valid-password-123", ["__RequestVerificationToken"] = token }), TestContext.Current.CancellationToken);
                Assert.Equal("/second-factor", login.Headers.Location?.OriginalString);
                Assert.DoesNotContain(transport.CookieContainer.GetCookies(client.BaseAddress).Cast<Cookie>(), cookie => cookie.Name == WorkflowSessionAuthenticationHandler.CookieName);
                var page = await client.GetStringAsync("/second-factor", TestContext.Current.CancellationToken);
                Assert.Contains("one-time-code", page, StringComparison.Ordinal);
                var pendingCookie = transport.CookieContainer.GetCookies(client.BaseAddress).Cast<Cookie>().Single(cookie => cookie.Name == "ew.second-factor");
                var pendingValue = pendingCookie.Value;
                using var rejected = await client.PostAsync("/auth/second-factor", Proof("incorrect", token), TestContext.Current.CancellationToken);
                Assert.Equal("/second-factor?error=verification", rejected.Headers.Location?.OriginalString);
                Assert.DoesNotContain(transport.CookieContainer.GetCookies(client.BaseAddress).Cast<Cookie>(), cookie => cookie.Name == WorkflowSessionAuthenticationHandler.CookieName);
                using var accepted = await client.PostAsync("/auth/second-factor", Proof("correct-proof", token), TestContext.Current.CancellationToken);
                Assert.Equal("/", accepted.Headers.Location?.OriginalString);
                var sessionCookie = transport.CookieContainer.GetCookies(client.BaseAddress).Cast<Cookie>().Single(cookie => cookie.Name == WorkflowSessionAuthenticationHandler.CookieName);
                var manager = host.Services.GetRequiredService<SecuritySessionManager>();
                Assert.Equal(SessionValidationStatus.Valid, (await manager.ValidateAsync(sessionCookie.Value, TestContext.Current.CancellationToken)).Status);
                // Replaying even the original protected pending cookie cannot consume the proof twice.
                transport.CookieContainer.Add(client.BaseAddress, new Cookie(WorkflowSessionAuthenticationHandler.CookieName, "", "/") { Expired = true });
                transport.CookieContainer.Add(client.BaseAddress, new Cookie("ew.second-factor", pendingValue, "/"));
                using var replay = await client.PostAsync("/auth/second-factor", Proof("correct-proof", token), TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
                Assert.Equal("/second-factor?error=verification", replay.Headers.Location?.OriginalString);
            }
            finally { await app.StopAsync(TestContext.Current.CancellationToken); }
        }
        finally { Directory.Delete(root, true); }
    }

    private static FormUrlEncodedContent Proof(string proof, string token) => new(new Dictionary<string, string>
    { ["proof"] = proof, ["__RequestVerificationToken"] = token });

    public sealed class SingleUseHandler : ISecondFactorHandler
    {
        private readonly object _gate = new();
        private SecondFactorContext? _pending;
        private int _attempts;
        public ValueTask<ProviderResult<SecondFactorChallenge>> BeginAsync(SecondFactorContext context, CancellationToken cancellationToken)
        {
            _pending = context;
            return ValueTask.FromResult(new ProviderResult<SecondFactorChallenge>(ProviderOutcome.Succeeded, new("challenge", DateTimeOffset.UtcNow.AddMinutes(1))));
        }
        public ValueTask<SecondFactorVerification> VerifyAsync(SecondFactorVerificationContext context, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var valid = _pending is not null && _pending.Identity == context.Identity && _pending.AttemptId == context.AttemptId
                    && context.ChallengeId == "challenge" && ++_attempts <= 3 && context.Proof == "correct-proof";
                if (valid) _pending = null;
                return ValueTask.FromResult(new SecondFactorVerification(valid ? SecondFactorStatus.Succeeded : SecondFactorStatus.Rejected));
            }
        }
        public ValueTask CancelAsync(SecondFactorContext context, string challengeId, CancellationToken cancellationToken)
        { _pending = null; return ValueTask.CompletedTask; }
    }
}

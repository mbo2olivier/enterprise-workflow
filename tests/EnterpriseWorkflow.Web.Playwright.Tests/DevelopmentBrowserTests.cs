using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace EnterpriseWorkflow.Web.Playwright.Tests;

public sealed class DevelopmentBrowserTests
{
    [Fact]
    public async Task DevelopmentMenuReflectsAdministrativePermission()
    {
        var address = Environment.GetEnvironmentVariable("EW_DEV_TEST_URL");
        if (string.IsNullOrWhiteSpace(address)) Assert.Skip("Set EW_DEV_TEST_URL to an ew dev runtime with the sample seed.");
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        foreach (var identity in new[] { "ew-dev-alice", "ew-admin" })
        {
            await page.GotoAsync(address + "/__ew/dev");
            await Expect(page.Locator(".login-panel")).ToBeVisibleAsync();
            await page.Locator("select[name=identity]").SelectOptionAsync(new SelectOptionValue { Label = identity == "ew-admin" ? "Administration locale" : "Alice — Demandeuse" });
            await page.GetByRole(AriaRole.Button, new() { Name = "Explorer le module" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Complementary, new() { Name = "Environnement de développement" })).ToBeVisibleAsync();
            await page.GetByLabel("Ouvrir le menu utilisateur").ClickAsync();
            await Expect(page.GetByRole(AriaRole.Menuitem, new() { Name = "Administration", Exact = true }))
                .ToHaveCountAsync(identity == "ew-admin" ? 1 : 0);
        }
    }

    [Fact]
    public async Task LeaveRequestOnDevelopmentRuntime()
    {
        var address = Environment.GetEnvironmentVariable("EW_DEV_TEST_URL");
        if (string.IsNullOrWhiteSpace(address)) Assert.Skip("Set EW_DEV_TEST_URL to a fresh ew dev LeaveRequest runtime with the sample seed.");
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var reason = "Qualification L9 " + Guid.NewGuid().ToString("N");
        var stylesheet = await page.APIRequest.GetAsync(address + "/_content/EnterpriseWorkflow.Web/enterprise-workflow.css");
        Assert.Equal(200, stylesheet.Status);
        Assert.Contains(".app-shell", await stylesheet.TextAsync(), StringComparison.Ordinal);
        await SelectAsync("ew-dev-alice");
        await page.GotoAsync(address + "/start/sample.leave-request.leave-request/1");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demande de congé", Exact = true })).ToBeVisibleAsync();
        await page.Locator("input[name='field.startDate']").FillAsync("2026-11-02");
        await page.Locator("input[name='field.endDate']").FillAsync("2026-11-04");
        await page.Locator("select[name='field.leaveType']").SelectOptionAsync("paid");
        await page.Locator("textarea[name='field.reason']").FillAsync(reason);
        await page.Locator("textarea[name='field.privateNote']").FillAsync("SECRET-L9-NOTE");
        await page.GetByRole(AriaRole.Button, new() { Name = "Envoyer la demande" }).ClickAsync();
        var instanceId = new Uri(page.Url).Query.Split("started=", StringSplitOptions.None).Last().Split('&')[0];
        Assert.True(Guid.TryParse(instanceId, out _), $"Instance was not created: {page.Url}");
        await OpenTaskAsync("Confirmation du demandeur");
        var compose = Environment.GetEnvironmentVariable("EW_SMTP_COMPOSE_FILE");
        var testDatabase = Environment.GetEnvironmentVariable("EW_DEV_TEST_DB");
        if (!string.IsNullOrWhiteSpace(compose))
        {
            Assert.False(string.IsNullOrWhiteSpace(testDatabase), "EW_DEV_TEST_DB is required for SMTP outage qualification.");
            await DockerAsync("stop");
        }
        try
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Confirmer", Exact = true }).ClickAsync();
            await SelectAsync("ew-dev-bob");
            await OpenTaskAsync("Validation RH");
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Prendre en charge" })).ToBeVisibleAsync();
            if (!string.IsNullOrWhiteSpace(compose))
            {
                var failurePersisted = false;
                await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = testDatabase, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                for (var attempt = 0; attempt < 80; attempt++)
                {
                    await using var query = connection.CreateCommand();
                    query.CommandText = "SELECT COUNT(*) FROM EwOutbox WHERE InstanceId = $id AND LastErrorCode IS NOT NULL";
                    query.Parameters.AddWithValue("$id", Guid.Parse(instanceId).ToString("N"));
                    if (Convert.ToInt64(await query.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0) { failurePersisted = true; break; }
                    await Task.Delay(250, TestContext.Current.CancellationToken);
                }
                Assert.True(failurePersisted, "SMTP failure must persist a retry without blocking the inbox.");
            }
        }
        finally { if (!string.IsNullOrWhiteSpace(compose)) await DockerAsync("start"); }

        var capture = Environment.GetEnvironmentVariable("EW_SMTP_TEST_URL");
        if (!string.IsNullOrWhiteSpace(capture))
        {
            using var client = new HttpClient();
            var delivered = false;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                var messages = await client.GetStringAsync(capture + "/api/v1/messages", TestContext.Current.CancellationToken);
                if (messages.Contains("bob@example.test", StringComparison.Ordinal) && messages.Contains(page.Url, StringComparison.Ordinal)) { delivered = true; break; }
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }
            Assert.True(delivered, "SMTP capture did not receive the RH notification for this task.");
        }
        Assert.DoesNotContain("SECRET-L9-NOTE", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
        var rhTaskUrl = page.Url;
        await SelectAsync("ew-dev-carol");
        await page.GotoAsync(rhTaskUrl);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Tâche indisponible", Exact = true })).ToBeVisibleAsync();
        await SelectAsync("ew-dev-bob");
        await page.GotoAsync(rhTaskUrl);
        await page.GetByRole(AriaRole.Button, new() { Name = "Prendre en charge" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Valider et désigner le responsable" }).ClickAsync();
        Assert.Contains("error=", page.Url, StringComparison.Ordinal);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Validation RH", Exact = true })).ToBeVisibleAsync();
        await page.GetByLabel("Rechercher un destinataire éligible").FillAsync("carol");
        await page.GetByRole(AriaRole.Button, new() { Name = "Rechercher", Exact = true }).ClickAsync();
        await page.Locator("select[name='assignment.validation-manager.identity']").SelectOptionAsync("local|ew-dev-carol");
        await page.GetByRole(AriaRole.Button, new() { Name = "Valider et désigner le responsable" }).ClickAsync();

        await SelectAsync("ew-dev-carol");
        await OpenTaskAsync("Validation du responsable");
        Assert.DoesNotContain("SECRET-L9-NOTE", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
        await page.GetByRole(AriaRole.Button, new() { Name = "Approuver", Exact = true }).ClickAsync();
        await SelectAsync("ew-dev-alice");
        await page.GotoAsync(address + "/requests");
        var completedRequest = page.Locator("article.request-card").Filter(new() { HasText = instanceId }).GetByText("Terminée", new() { Exact = true });
        for (var attempt = 0; attempt < 20 && await completedRequest.CountAsync() == 0; attempt++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            await page.ReloadAsync();
        }
        await Expect(completedRequest).ToBeVisibleAsync();

        var smtp = Environment.GetEnvironmentVariable("EW_SMTP_TEST_URL");
        if (!string.IsNullOrWhiteSpace(smtp))
        {
            using var client = new HttpClient();
            string messages = "";
            for (var attempt = 0; attempt < 30; attempt++)
            {
                messages = await client.GetStringAsync(smtp + "/api/v1/messages", TestContext.Current.CancellationToken);
                if (messages.Contains("bob@example.test", StringComparison.Ordinal)) break;
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }
            Assert.Contains("bob@example.test", messages, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET-L9-NOTE", messages, StringComparison.Ordinal);
        }
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "ew-l9-browser.png"), FullPage = true });

        async Task DockerAsync(string action)
        {
            var start = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "compose", "-f", compose!, action, "smtp" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(process.ExitCode == 0, await output + await error);
        }

        async Task SelectAsync(string identity)
        {
            await page.GotoAsync(address + "/__ew/dev");
            await page.Locator("select[name=identity]").SelectOptionAsync(identity);
            await page.GetByRole(AriaRole.Button, new() { Name = "Explorer le module" }).ClickAsync();
            await Expect(page).ToHaveURLAsync(address + "/");
        }
        async Task OpenTaskAsync(string label)
        {
            await page.GotoAsync(address + "/inbox");
            var nodeId = label switch { "Confirmation du demandeur" => "draft", "Validation RH" => "validation-rh", _ => "validation-manager" };
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var links = await page.Locator("article.request-card").Filter(new() { HasText = nodeId }).GetByRole(AriaRole.Link, new() { Name = "Consulter →" }).EvaluateAllAsync<string[]>("links => links.map(link => link.href)");
                foreach (var link in links)
                {
                    await page.GotoAsync(link);
                    if ((await page.Locator("main").InnerTextAsync()).Contains(reason, StringComparison.Ordinal))
                    {
                        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = label, Exact = true })).ToBeVisibleAsync();
                        return;
                    }
                }
                await Task.Delay(250, TestContext.Current.CancellationToken);
                await page.GotoAsync(address + "/inbox");
            }
            Assert.Fail($"No task for this request: {label}");
        }
    }
    [Theory]
    [InlineData("cancel")]
    [InlineData("rh")]
    [InlineData("manager")]
    public async Task LeaveRequestBusinessBranches(string branch)
    {
        var address = Environment.GetEnvironmentVariable("EW_DEV_TEST_URL");
        if (string.IsNullOrWhiteSpace(address)) Assert.Skip("Set EW_DEV_TEST_URL to the LeaveRequest development runtime.");
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var reason = "Branch " + Guid.NewGuid().ToString("N");
        await SelectAsync("ew-dev-alice");
        await page.GotoAsync(address + "/start/sample.leave-request.leave-request/1");
        await page.Locator("input[name='field.startDate']").FillAsync("2026-11-02");
        await page.Locator("input[name='field.endDate']").FillAsync("2026-11-04");
        await page.Locator("select[name='field.leaveType']").SelectOptionAsync("paid");
        await page.Locator("textarea[name='field.reason']").FillAsync(reason);
        await page.GetByRole(AriaRole.Button, new() { Name = "Envoyer la demande" }).ClickAsync();
        var instanceId = new Uri(page.Url).Query.Split("started=", StringSplitOptions.None).Last().Split('&')[0];
        Assert.True(Guid.TryParse(instanceId, out _));
        await OpenAsync("draft");
        if (branch == "cancel")
            await page.GetByRole(AriaRole.Button, new() { Name = "Annuler", Exact = true }).ClickAsync();
        else
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Confirmer", Exact = true }).ClickAsync();
            await SelectAsync("ew-dev-bob");
            await OpenAsync("validation-rh");
            await page.GetByRole(AriaRole.Button, new() { Name = "Prendre en charge" }).ClickAsync();
            if (branch == "manager")
            {
                await page.GetByLabel("Rechercher un destinataire éligible").FillAsync("carol");
                await page.GetByRole(AriaRole.Button, new() { Name = "Rechercher", Exact = true }).ClickAsync();
                await page.Locator("select[name='assignment.validation-manager.identity']").SelectOptionAsync("local|ew-dev-carol");
                await page.GetByRole(AriaRole.Button, new() { Name = "Valider et désigner le responsable" }).ClickAsync();
                await SelectAsync("ew-dev-carol");
                await OpenAsync("validation-manager");
            }
            await page.GetByRole(AriaRole.Button, new() { Name = "Refuser", Exact = true }).ClickAsync();
            Assert.Contains("error=", page.Url, StringComparison.Ordinal);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Refuser", Exact = true })).ToBeVisibleAsync();
            await page.Locator("textarea[name='field.comment']").FillAsync("Refus motivé de qualification");
            await page.GetByRole(AriaRole.Button, new() { Name = "Refuser", Exact = true }).ClickAsync();
            Assert.Contains("completed=1", page.Url, StringComparison.Ordinal);
        }
        await SelectAsync("ew-dev-alice");
        await page.GotoAsync(address + "/requests");
        var completed = page.Locator("article.request-card").Filter(new() { HasText = instanceId }).GetByText("Terminée", new() { Exact = true });
        for (var attempt = 0; attempt < 20 && await completed.CountAsync() == 0; attempt++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            await page.ReloadAsync();
        }
        await Expect(completed).ToBeVisibleAsync();

        async Task SelectAsync(string id)
        {
            await page.GotoAsync(address + "/__ew/dev");
            await page.Locator("select[name=identity]").SelectOptionAsync(id);
            await page.GetByRole(AriaRole.Button, new() { Name = "Explorer le module" }).ClickAsync();
        }
        async Task OpenAsync(string node)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                await page.GotoAsync(address + "/inbox");
                var links = await page.Locator("article.request-card").Filter(new() { HasText = node }).GetByRole(AriaRole.Link, new() { Name = "Consulter →" }).EvaluateAllAsync<string[]>("links => links.map(link => link.href)");
                foreach (var link in links)
                {
                    await page.GotoAsync(link);
                    if ((await page.Locator("main").InnerTextAsync()).Contains(reason, StringComparison.Ordinal)) return;
                }
                await Task.Delay(250, TestContext.Current.CancellationToken);
            }
            Assert.Fail("Task not available for this request: " + node);
        }
    }

}

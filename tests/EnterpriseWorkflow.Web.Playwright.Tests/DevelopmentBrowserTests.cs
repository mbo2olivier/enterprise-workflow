using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace EnterpriseWorkflow.Web.Playwright.Tests;

public sealed class DevelopmentBrowserTests
{
    [Fact]
    public async Task SearchMultiselectAndAdministrativeEditorsRemainUsable()
    {
        var address = Environment.GetEnvironmentVariable("EW_DEV_TEST_URL");
        if (string.IsNullOrWhiteSpace(address)) Assert.Skip("Set EW_DEV_TEST_URL to the sample development runtime.");
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1046, Height = 783 } });
        await Select("Alice — Demandeuse");
        await page.GetByRole(AriaRole.Button, new() { Name = "Rechercher des demandes", Exact = true }).ClickAsync();
        var search = page.Locator("dialog[data-search-dialog]");
        await search.Locator(".status-dropdown summary").ClickAsync();
        await search.GetByRole(AriaRole.Checkbox, new() { Name = "En attente", Exact = true }).CheckAsync();
        await search.GetByRole(AriaRole.Checkbox, new() { Name = "Terminée", Exact = true }).CheckAsync();
        await search.Locator(".status-dropdown summary").ClickAsync();
        await Expect(search.Locator("[data-status-summary]")).ToHaveTextAsync("En attente, Terminée");
        await search.GetByRole(AriaRole.Button, new() { Name = "Rechercher", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("status=Waiting.*status=Completed"));
        await Expect(page.Locator("form.filter-form > .filter-actions button[type=submit]")).ToBeVisibleAsync();
        await Select("Administration locale");
        await page.GotoAsync(address + "/admin/access?tab=identities");
        Assert.True(await page.Locator(".identity-action-menu summary").CountAsync() >= 4);
        await page.GotoAsync(address + "/admin/access?tab=profiles&profile=administrators");
        var editor = page.Locator("dialog[data-promoted]");
        await Expect(editor).ToBeVisibleAsync();
        Assert.True(await editor.EvaluateAsync<bool>("element => element.matches(':modal')"));
        var heading = await page.GetByRole(AriaRole.Heading, new() { Name = "Administrateurs", Exact = true }).BoundingBoxAsync();
        Assert.NotNull(heading);
        Assert.InRange(heading.Y, 0, 783);
        await page.ScreenshotAsync(new() { Path = "/private/tmp/ew-l9b-profile-modal.png" });
        await page.GetByRole(AriaRole.Link, new() { Name = "Fermer la fenêtre", Exact = true }).ClickAsync();
        await page.GotoAsync(address + "/admin/audit");
        await Expect(page.Locator("form.filter-form > .filter-actions").GetByRole(AriaRole.Button, new() { Name = "Exporter les résultats CSV", Exact = true })).ToBeEnabledAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Rechercher des demandes", Exact = true }).ClickAsync();
        await page.ScreenshotAsync(new() { Path = "/private/tmp/ew-l9b-search-modal.png" });
        async Task Select(string label)
        {
            await page.GotoAsync(address + "/__ew/dev?switch=" + Guid.NewGuid().ToString("N"));
            await page.Locator("select[name=identity]").SelectOptionAsync(new SelectOptionValue { Label = label });
            await page.GetByRole(AriaRole.Button, new() { Name = "Explorer le module", Exact = true }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Complementary, new() { Name = "Environnement de développement" })).ToBeVisibleAsync();
        }
    }

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
            await page.GotoAsync(address + "/__ew/dev?switch=" + Guid.NewGuid().ToString("N"));
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
        await page.GotoAsync(address + "/start");
        await page.Locator("a.catalog-card").Filter(new() { HasText = "Demande de congé" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demande de congé", Exact = true })).ToBeVisibleAsync();
        await page.Locator("input[name='field.startDate']").FillAsync("2026-11-02");
        await page.Locator("input[name='field.endDate']").FillAsync("2026-11-04");
        await page.Locator("select[name='field.leaveType']").SelectOptionAsync("paid");
        await page.Locator("textarea[name='field.reason']").FillAsync(reason);
        await page.Locator("textarea[name='field.privateNote']").FillAsync("SECRET-L9-NOTE");
        await page.Locator("input[data-media-upload='documents']").SetInputFilesAsync(new FilePayload { Name = "justificatif.txt", MimeType = "text/plain", Buffer = System.Text.Encoding.UTF8.GetBytes("Preuve L9b") });
        await Expect(page.Locator("[data-media-status='documents']")).ToContainTextAsync("validé");
        await page.GetByRole(AriaRole.Button, new() { Name = "Envoyer la demande" }).ClickAsync();
        var instanceId = new Uri(page.Url).Query.Split("started=", StringSplitOptions.None).Last().Split('&')[0];
        Assert.True(Guid.TryParse(instanceId, out _), $"Instance was not created: {page.Url}");
        await OpenTaskAsync("Confirmation du demandeur");
        await Expect(page.Locator("[data-form-view=LeaveRequestSummary]")).ToContainTextAsync(reason);
        // The requester must designate the first approver before submission.
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirmer", Exact = true }).ClickAsync();
        Assert.Contains("error=", page.Url, StringComparison.Ordinal);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Confirmation du demandeur", Exact = true })).ToBeVisibleAsync();
        var picker = page.Locator(".candidate-picker").Filter(new() { Has = page.Locator("input[name='assignment.validation-manager.identity']") });
        await picker.GetByRole(AriaRole.Combobox).FillAsync("car");
        await picker.GetByRole(AriaRole.Option).Filter(new() { HasText = "carol" }).ClickAsync();
        await Expect(picker.Locator("input[data-candidate-value]")).ToHaveValueAsync("local|ew-dev-carol");
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirmer", Exact = true }).ClickAsync();
        await SelectAsync("ew-dev-bob");
        await page.GotoAsync(address + "/inbox");
        await Expect(page.Locator("a.request-card[data-stage='validation-rh']")).ToHaveCountAsync(0);
        await SelectAsync("ew-dev-carol");
        await OpenTaskAsync("Validation du responsable");
        Assert.DoesNotContain("SECRET-L9-NOTE", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
        var managerTaskUrl = page.Url;
        await SelectAsync("ew-dev-bob");
        await page.GotoAsync(managerTaskUrl);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Tâche indisponible", Exact = true })).ToBeVisibleAsync();
        await SelectAsync("ew-dev-carol");
        await page.GotoAsync(managerTaskUrl);
        var compose = Environment.GetEnvironmentVariable("EW_SMTP_COMPOSE_FILE");
        var testDatabase = Environment.GetEnvironmentVariable("EW_DEV_TEST_DB");
        if (!string.IsNullOrWhiteSpace(compose))
        {
            Assert.False(string.IsNullOrWhiteSpace(testDatabase), "EW_DEV_TEST_DB is required for SMTP outage qualification.");
            await DockerAsync("stop");
        }
        try
        {
            // HR pool notifications are queued only after manager approval.
            await page.GetByRole(AriaRole.Button, new() { Name = "Approuver", Exact = true }).ClickAsync();
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
        await page.GetByRole(AriaRole.Button, new() { Name = "Valider", Exact = true }).ClickAsync();
        await SelectAsync("ew-dev-alice");
        await page.GotoAsync(address + "/requests");
        var completedRequest = page.Locator("a.request-card[data-instance-id='" + instanceId + "']").GetByText("Terminée", new() { Exact = true });
        for (var attempt = 0; attempt < 20 && await completedRequest.CountAsync() == 0; attempt++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            await page.ReloadAsync();
        }
        await Expect(completedRequest).ToBeVisibleAsync();
        await page.Locator("a.request-card[data-instance-id='" + instanceId + "']").ClickAsync();
        await Expect(page).ToHaveURLAsync(address + "/instances/" + instanceId);
        Assert.DoesNotContain("SECRET-L9-NOTE", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
        await Expect(page.Locator("#instance-reference")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^[0-9A-Z]{10}$"));
        var publicReference = await page.Locator("#instance-reference").InnerTextAsync();
        await Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Onglets de la demande" }).GetByRole(AriaRole.Link)).ToHaveCountAsync(5);
        await Expect(page.GetByRole(AriaRole.Rowheader, new() { Name = "Commentaires", Exact = true })).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Rowheader, new() { Name = "Justificatifs", Exact = true })).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Link, new() { Name = "Bibliothèque", Exact = true }).ClickAsync();
        var upload = page.GetByRole(AriaRole.Link, new() { Name = "justificatif.txt", Exact = true });
        await Expect(upload).ToBeVisibleAsync();
        var uploadPath = await upload.GetAttributeAsync("href");
        var download = await page.APIRequest.GetAsync(address + uploadPath);
        Assert.Equal("Preuve L9b", await download.TextAsync());
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "cloture.txt", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Rechercher des demandes", Exact = true }).ClickAsync();
        var dialog = page.Locator("dialog[data-search-dialog]");
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.Locator("input[name=instance]").FillAsync(publicReference);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Rechercher", Exact = true }).ClickAsync();
        await Expect(page.Locator("a.request-card")).ToHaveCountAsync(1);
        await page.GotoAsync(address + "/search?provider=local&initiator=ew-dev-bob");
        await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Recherche impossible");
        await SelectAsync("ew-admin");
        await page.GotoAsync(address + "/instances/" + instanceId);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demande indisponible", Exact = true })).ToBeVisibleAsync();
        var deniedDownload = await page.APIRequest.GetAsync(address + uploadPath);
        Assert.Equal(404, deniedDownload.Status);
        await page.GotoAsync(address + "/admin/audit");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Exporter les résultats CSV", Exact = true })).ToBeVisibleAsync();
        var auditExport = await page.APIRequest.GetAsync(address + "/admin/audit/export?action=security.profile.upsert");
        Assert.Equal(200, auditExport.Status);
        Assert.Contains("text/csv", auditExport.Headers["content-type"], StringComparison.Ordinal);
        await page.GotoAsync(address + "/admin/audit?module=sample.leave-request&process=sample.leave-request.leave-request&action=validate");
        await Expect(page.Locator(".data-panel table")).ToContainTextAsync(publicReference);
        var businessAuditExport = await page.APIRequest.GetAsync(address + "/admin/audit/export?module=sample.leave-request&process=sample.leave-request.leave-request&action=validate");
        Assert.Equal(200, businessAuditExport.Status);
        Assert.Contains(publicReference, await businessAuditExport.TextAsync(), StringComparison.Ordinal);
        await page.GotoAsync(address + "/admin/tat");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Temps de traitement", Exact = true })).ToBeVisibleAsync();
        var tatExport = await page.APIRequest.GetAsync(address + "/admin/tat/export");
        Assert.Equal(200, tatExport.Status);
        Assert.Contains("validation-rh", await tatExport.TextAsync(), StringComparison.Ordinal);

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
            await page.GotoAsync(address + "/__ew/dev?switch=" + Guid.NewGuid().ToString("N"));
            await page.Locator("select[name=identity]").SelectOptionAsync(identity == "ew-admin" ? new SelectOptionValue { Label = "Administration locale" } : new SelectOptionValue { Value = identity });
            await page.GetByRole(AriaRole.Button, new() { Name = "Explorer le module" }).ClickAsync();
            await Expect(page).ToHaveURLAsync(address + "/");
            await Expect(page.GetByRole(AriaRole.Complementary, new() { Name = "Environnement de développement" })).ToContainTextAsync(identity);
        }
        async Task OpenTaskAsync(string label)
        {
            await page.GotoAsync(address + "/inbox");
            var nodeId = label switch { "Confirmation du demandeur" => "draft", "Validation RH" => "validation-rh", _ => "validation-manager" };
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var links = await page.Locator("a.request-card[data-stage='" + nodeId + "']").EvaluateAllAsync<string[]>("links => links.map(link => link.href)");
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
        await page.GotoAsync(address + "/start");
        await page.Locator("a.catalog-card").Filter(new() { HasText = "Demande de congé" }).ClickAsync();
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
            var picker = page.Locator(".candidate-picker").Filter(new() { Has = page.Locator("input[name='assignment.validation-manager.identity']") });
            await picker.GetByRole(AriaRole.Combobox).FillAsync("car");
            await picker.GetByRole(AriaRole.Option).Filter(new() { HasText = "carol" }).ClickAsync();
            await Expect(picker.Locator("input[data-candidate-value]")).ToHaveValueAsync("local|ew-dev-carol");
            await page.GetByRole(AriaRole.Button, new() { Name = "Confirmer", Exact = true }).ClickAsync();
            await SelectAsync("ew-dev-carol");
            await OpenAsync("validation-manager");
            if (branch == "rh")
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Approuver", Exact = true }).ClickAsync();
                await SelectAsync("ew-dev-bob");
                await OpenAsync("validation-rh");
                await page.GetByRole(AriaRole.Button, new() { Name = "Prendre en charge" }).ClickAsync();
            }
            await page.GetByRole(AriaRole.Button, new() { Name = "Refuser", Exact = true }).ClickAsync();
            Assert.Contains("error=", page.Url, StringComparison.Ordinal);
            await Expect(page.Locator(".validation-summary")).ToContainTextAsync("Un commentaire est obligatoire");
            await Expect(page.Locator("textarea[name='field.comment']")).ToHaveAttributeAsync("aria-invalid", "true");
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Refuser", Exact = true })).ToBeVisibleAsync();
            await page.Locator("textarea[name='field.comment']").FillAsync("Refus motivé de qualification");
            await page.GetByRole(AriaRole.Button, new() { Name = "Refuser", Exact = true }).ClickAsync();
            Assert.Contains("completed=1", page.Url, StringComparison.Ordinal);
        }
        await SelectAsync("ew-dev-alice");
        await page.GotoAsync(address + "/requests");
        var completed = page.Locator("a.request-card[data-instance-id='" + instanceId + "']").GetByText(branch == "cancel" ? "Annulée" : "Refusée", new() { Exact = true });
        for (var attempt = 0; attempt < 20 && await completed.CountAsync() == 0; attempt++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            await page.ReloadAsync();
        }
        await Expect(completed).ToBeVisibleAsync();
        await page.Locator("a.request-card[data-instance-id='" + instanceId + "']").ClickAsync();
        if (branch is "rh" or "manager") { await page.GetByRole(AriaRole.Link, new() { Name = "Commentaires", Exact = true }).ClickAsync(); await Expect(page.Locator("main")).ToContainTextAsync("Refus motivé de qualification"); }

        async Task SelectAsync(string id)
        {
            await page.GotoAsync(address + "/__ew/dev?switch=" + Guid.NewGuid().ToString("N"));
            await page.Locator("select[name=identity]").SelectOptionAsync(id);
            await page.GetByRole(AriaRole.Button, new() { Name = "Explorer le module" }).ClickAsync();
            await Expect(page).ToHaveURLAsync(address + "/");
            await Expect(page.GetByRole(AriaRole.Complementary, new() { Name = "Environnement de développement" })).ToContainTextAsync(id);
        }
        async Task OpenAsync(string node)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                await page.GotoAsync(address + "/inbox");
                var links = await page.Locator("a.request-card[data-stage='" + node + "']").EvaluateAllAsync<string[]>("links => links.map(link => link.href)");
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

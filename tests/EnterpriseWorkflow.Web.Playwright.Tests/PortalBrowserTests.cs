using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace EnterpriseWorkflow.Web.Playwright.Tests;

public sealed class PortalBrowserTests
{
    private static readonly string[] FocusableTags = ["A", "BUTTON", "INPUT", "SELECT"];

    [Fact]
    public async Task PortalAndAdministrationAreQualifiedOnSqlite()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_ENABLED"), "1", StringComparison.Ordinal))
            Assert.Skip("Playwright qualification runs only in the dedicated Linux job.");

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        var repository = FindRepositoryRoot();
        var kernel = Path.Combine(repository, "src", "EnterpriseWorkflow.Kernel", "bin", "Release", "net10.0",
            "EnterpriseWorkflow.Kernel.dll");
        var database = Path.Combine(Path.GetTempPath(), $"enterprise-workflow-browser-{Guid.NewGuid():N}.db");
        var port = FreePort();
        var baseAddress = $"http://127.0.0.1:{port}";
        const string password = "BrowserQualification_2026";
        var hostLog = new StringBuilder();
        Process? host = null;
        try
        {
            await RunKernelCommandAsync(kernel, database, "migrate");
            await RunKernelCommandAsync(kernel, database, "bootstrap", "browser-admin", password);
            host = StartKernel(kernel, database, baseAddress, hostLog);
            await WaitUntilReadyAsync(baseAddress, host, hostLog);

            await page.GotoAsync($"{baseAddress}/login");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Se connecter" })).ToBeVisibleAsync();
            await page.Mouse.MoveAsync(280, 220);
            await Expect(page.Locator(".login-page")).ToHaveAttributeAsync("style", new Regex("--pointer-x:"));
            await Expect(page.Locator(".login-grid-reveal")).ToHaveCountAsync(1);
            await page.Locator("input[name=userName]").FillAsync("browser-admin");
            await page.Locator("input[name=password]").FillAsync(password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Se connecter" }).ClickAsync();
            await Expect(page).ToHaveURLAsync(baseAddress + "/");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hello, browser-admin" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "À faire maintenant" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Mes demandes récentes" })).ToBeVisibleAsync();
            Assert.Equal(4, await page.Locator(".sidebar .nav-item svg").CountAsync());
            Assert.Equal(0, await page.Locator(".topbar > form[action='/auth/logout']").CountAsync());

            // The login POST creates a new document. SSR content can be visible before
            // its scripts attach event listeners, so wait before sending the pointer event.
            await page.WaitForLoadStateAsync(LoadState.Load);
            await page.Mouse.MoveAsync(600, 300);
            await Expect(page.Locator(".workspace")).ToHaveAttributeAsync("style", new Regex("--pointer-x:"));

            await page.Locator("summary[aria-label='Ouvrir le menu utilisateur']").ClickAsync();
            await Expect(page.GetByRole(AriaRole.Menuitem, new() { Name = "Administration" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Menuitem, new() { Name = "Se déconnecter" })).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Menuitem, new() { Name = "Administration" }).ClickAsync();
            await Expect(page).ToHaveURLAsync(baseAddress + "/admin");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Administration", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(4, await page.Locator(".admin-health-grid .health-card").CountAsync());
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Requiert votre attention" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Derniers changements" })).ToBeVisibleAsync();

            await page.GotoAsync(baseAddress + "/admin/access");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Accès", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(3, await page.Locator(".segmented-control a").CountAsync());
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Nouveau profil" })).ToBeVisibleAsync();
            Assert.Equal(1, await page.Locator(".access-profile-table").CountAsync());
            Assert.Equal(0, await page.Locator(".metric-grid, .data-panel").CountAsync());
            await page.GetByRole(AriaRole.Link, new() { Name = "Nouveau profil" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Heading, new() { Name = "Nouveau profil" })).ToBeVisibleAsync();
            Assert.Equal(1, await page.Locator(".access-profile-table").CountAsync());
            await page.GetByRole(AriaRole.Link, new() { Name = "Fermer la fenêtre" }).ClickAsync();

            await page.GetByRole(AriaRole.Link, new() { Name = "Identités", Exact = true }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Nouvelle identité" })).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Nouvelle identité" }).ClickAsync();
            var identityDialog = page.GetByRole(AriaRole.Dialog);
            await Expect(identityDialog.GetByRole(AriaRole.Heading, new() { Name = "Nouvelle identité" })).ToBeVisibleAsync();
            await Expect(identityDialog.Locator("form[action='/admin/access/accounts']")).ToHaveCountAsync(1);
            await identityDialog.GetByLabel("Identifiant de connexion").FillAsync("browser-user");
            await identityDialog.GetByLabel("Mot de passe temporaire").FillAsync("BrowserIdentity_2026");
            await identityDialog.GetByRole(AriaRole.Button, new() { Name = "Créer l’identité" }).ClickAsync();
            await Expect(page).ToHaveURLAsync(baseAddress + "/admin/access?tab=identities&saved=1");
            await Expect(page.Locator(".identity-table").GetByText("browser-user", new() { Exact = true })).ToBeVisibleAsync();

            await page.GotoAsync(baseAddress + "/admin/operations");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "État de l’installation" })).ToBeVisibleAsync();
            Assert.Equal(4, await page.Locator(".operations-strip > div").CountAsync());
            Assert.Equal(3, await page.Locator(".segmented-control a").CountAsync());
            Assert.Equal(1, await page.Locator(".operations-table").CountAsync());

            await page.GotoAsync(baseAddress + "/admin/settings");
            Assert.Equal(3, await page.Locator(".settings-panel").CountAsync());
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Politique de sécurité" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Runtime" })).ToBeVisibleAsync();
            await page.Locator("input[name=displayName]").FillAsync("Portail de qualification");
            await page.Locator("input[name=accentColor]").FillAsync("#2457A7");
            var form = page.Locator("form[action='/admin/settings/update']");
            Assert.True(await form.EvaluateAsync<bool>("form => form.checkValidity()"),
                "The settings form is invalid before submission.");
            IResponse? mutationResponse = null;
            page.Response += (_, response) =>
            {
                if (response.Request.Method == "POST" && response.Url.EndsWith("/admin/settings/update", StringComparison.Ordinal))
                    mutationResponse = response;
            };
            await page.GetByRole(AriaRole.Button, new() { Name = "Enregistrer les réglages" }).ClickAsync();
            var status = page.GetByRole(AriaRole.Status);
            var alerts = await page.GetByRole(AriaRole.Alert).AllTextContentsAsync();
            var mutationStatus = mutationResponse is null
                ? "not observed"
                : mutationResponse.Status.ToString(CultureInfo.InvariantCulture);
            Assert.True(await status.CountAsync() > 0,
                $"Settings submission did not render a success status. POST status: {mutationStatus}. URL: {page.Url}. Alerts: {string.Join(" | ", alerts)}. Kernel log: {ReadLog(hostLog)}");
            await Expect(status).ToContainTextAsync("Réglages enregistrés");
            await Expect(page.Locator("body")).ToHaveAttributeAsync("style", "--accent:#2457A7");

            await page.SetViewportSizeAsync(390, 844);
            var fitsViewport = await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1");
            Assert.True(fitsViewport);
            await page.Keyboard.PressAsync("Tab");
            var activeTag = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.tagName");
            Assert.Contains(activeTag, FocusableTags);
        }
        finally
        {
            if (host is { HasExited: false }) host.Kill(entireProcessTree: true);
            host?.Dispose();
            foreach (var file in new[] { database, database + "-wal", database + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private static async Task RunKernelCommandAsync(string kernel, string database, params string[] arguments)
    {
        using var process = CreateProcess(kernel, database, null, arguments);
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"Kernel command failed ({process.ExitCode}). {output} {error}");
    }

    private static Process StartKernel(string kernel, string database, string baseAddress, StringBuilder log)
    {
        var process = CreateProcess(kernel, database, baseAddress, []);
        process.OutputDataReceived += (_, args) => AppendLog(log, args.Data);
        process.ErrorDataReceived += (_, args) => AppendLog(log, args.Data);
        process.Start();
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }

    private static Process CreateProcess(string kernel, string database, string? baseAddress, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.ArgumentList.Add(kernel);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["Database__Provider"] = "Sqlite";
        start.Environment["Database__MigrationMode"] = "Apply";
        start.Environment["ConnectionStrings__EnterpriseWorkflow"] = $"Data Source={database};Pooling=False";
        start.Environment["Security__AuthenticationProvider"] = "local";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        if (baseAddress is not null) start.Environment["ASPNETCORE_URLS"] = baseAddress;
        return new Process { StartInfo = start };
    }

    private static async Task WaitUntilReadyAsync(string baseAddress, Process host, StringBuilder log)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (host.HasExited)
                throw new InvalidOperationException($"Kernel exited with code {host.ExitCode}. Log: {ReadLog(log)}");
            try { if ((await client.GetAsync(baseAddress + "/login")).StatusCode is HttpStatusCode.OK) return; }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("Kernel did not become ready for browser qualification.");
    }

    private static void AppendLog(StringBuilder log, string? line)
    {
        if (line is null) return;
        lock (log) log.AppendLine(line);
    }

    private static string ReadLog(StringBuilder log)
    {
        lock (log) return log.ToString();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EnterpriseWorkflow.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}

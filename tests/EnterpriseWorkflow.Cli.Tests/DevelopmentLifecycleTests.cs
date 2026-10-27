using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
namespace EnterpriseWorkflow.Cli.Tests;

public sealed partial class DevelopmentLifecycleTests
{
    [Fact]
    public async Task SourceGenerationsPreserveInstancesAndNamedEnvironmentsAreIsolated()
    {
        if (Environment.GetEnvironmentVariable("EW_DEV_LIFECYCLE_TESTS") != "1") Assert.Skip("Opt-in native CLI lifecycle: set EW_DEV_LIFECYCLE_TESTS=1 (builds a sample several times).");
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "EnterpriseWorkflow.slnx"))) repo = repo.Parent;
        Assert.NotNull(repo);
        var cli = Path.Combine(repo.FullName, "src", "EnterpriseWorkflow.Cli", "bin", new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net10.0", "EnterpriseWorkflow.Cli.dll");
        var root = Path.Combine(OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(), "ew-native-lifecycle-" + Guid.NewGuid().ToString("N"));
        var module = Path.Combine(root, "Module"); Directory.CreateDirectory(module);
        var source = Path.Combine(repo.FullName, "samples", "LeaveRequest.Module");
        foreach (var file in Directory.GetFiles(source).Where(path => Path.GetExtension(path) is ".cs" or ".razor" or ".csproj" || Path.GetFileName(path) == "ew.dev.json")) File.Copy(file, Path.Combine(module, Path.GetFileName(file)));
        foreach (var name in new[] { "Directory.Build.props", "Directory.Packages.props", "global.json", "NuGet.Config" }) File.Copy(Path.Combine(repo.FullName, name), Path.Combine(root, name));
        var project = Directory.GetFiles(module, "*.csproj").Single(); var xml = XDocument.Load(project);
        foreach (var reference in xml.Descendants("ProjectReference")) reference.SetAttributeValue("Include", Path.GetFullPath(reference.Attribute("Include")!.Value, source));
        xml.Save(project);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var url = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() }; using var client = new HttpClient(handler) { BaseAddress = new Uri(url) };
        var token = TestContext.Current.CancellationToken;
        var first = await Start("first");
        string instance;
        string version;
        try
        {
            await Login(); version = await Version();
            var html = await client.GetStringAsync("/start/sample.leave-request.leave-request/" + version, token);
            var fields = Hidden(html); fields["field.startDate"] = "2026-11-02"; fields["field.endDate"] = "2026-11-04"; fields["field.leaveType"] = "paid"; fields["field.reason"] = "Native lifecycle";
            using var response = await client.PostAsync("/workflow/start/sample.leave-request.leave-request/" + version, new FormUrlEncodedContent(fields), token);
            response.EnsureSuccessStatusCode();
            instance = Regex.Match(response.RequestMessage!.RequestUri!.Query, "started=([a-fA-F0-9-]+)").Groups[1].Value;
            Assert.True(Guid.TryParse(instance, out _));
        }
        finally { await Stop(first); }
        var unused = Path.Combine(module, ".ew", "environments", "first", "artifacts", "unused"); Directory.CreateDirectory(unused);
        await File.WriteAllTextAsync(Path.Combine(unused, "module.json"), "unreadable unused prototype", token);
        var code = Path.Combine(module, "LeaveRequestModule.cs");
        await File.WriteAllTextAsync(code, (await File.ReadAllTextAsync(code, token)).Replace("Confirmer sa demande, puis", "Nouvelle génération : confirmer sa demande, puis", StringComparison.Ordinal), token);
        var second = await Start("first");
        try
        {
            await Login(); Assert.NotEqual(version, await Version());
            var detail = await client.GetStringAsync("/instances/" + instance, token); Assert.Contains("Native lifecycle", detail, StringComparison.Ordinal);
            var inbox = string.Empty; var task = string.Empty;
            for (var attempt = 0; attempt < 120; attempt++)
            {
                inbox = await client.GetStringAsync("/inbox", token); task = TaskLink().Match(inbox).Groups[1].Value;
                if (task.Length > 0) break;
                await Task.Delay(250, token);
            }
            Assert.True(task.Length > 0, "The restored instance did not expose its task within 30 seconds. Inbox: " + inbox);
            var taskHtml = await client.GetStringAsync("/tasks/" + task, token); var fields = Hidden(taskHtml); fields["action"] = "cancel";
            using var complete = await client.PostAsync("/workflow/tasks/" + task + "/action", new FormUrlEncodedContent(fields), token);
            complete.EnsureSuccessStatusCode(); Assert.Contains("completed=1", complete.RequestMessage!.RequestUri!.Query, StringComparison.Ordinal);
        }
        finally { await Stop(second); }
        var registry = Path.Combine(module, ".ew", "environments", "first", "generations.json");
        using (var generations = JsonDocument.Parse(await File.ReadAllTextAsync(registry, token))) Assert.Equal(2, generations.RootElement.GetArrayLength());
        var third = await Start("first");
        try { await Login(); Assert.Contains("Native lifecycle", await client.GetStringAsync("/instances/" + instance, token), StringComparison.Ordinal); }
        finally { await Stop(third); }
        using (var generations = JsonDocument.Parse(await File.ReadAllTextAsync(registry, token))) Assert.Equal(2, generations.RootElement.GetArrayLength());
        foreach (Cookie cookie in handler.CookieContainer.GetAllCookies()) cookie.Expired = true;
        var isolated = await Start("other");
        try { await Login(); Assert.Contains("Demande indisponible", await client.GetStringAsync("/instances/" + instance, token), StringComparison.Ordinal); }
        finally { await Stop(isolated); }
        var beforeReset = await File.ReadAllTextAsync(registry, token);
        var reset = await Start("other", reset: true);
        try { await Login(); Assert.Contains("Demande indisponible", await client.GetStringAsync("/instances/" + instance, token), StringComparison.Ordinal); }
        finally { await Stop(reset); }
        Assert.Equal(beforeReset, await File.ReadAllTextAsync(registry, token));
        Assert.Contains(Directory.GetDirectories(Path.Combine(module, ".ew", "archives")), path => Path.GetFileName(path).StartsWith("other-", StringComparison.Ordinal) && File.Exists(Path.Combine(path, "workflow.db")));
        var packageOutput = Path.Combine(root, "packages");
        var pack = await RunCli("pack", project, "--configuration", "Release", "--output", packageOutput);
        Assert.Equal(0, pack.Code);
        var packaged = Directory.GetFiles(packageOutput, "module.json", SearchOption.AllDirectories).Single();
        string releaseInstance;
        var released = await Start("first", Path.GetDirectoryName(packaged)!);
        try
        {
            await Login(); var releasedVersion = await Version(); Assert.True(int.Parse(releasedVersion, System.Globalization.CultureInfo.InvariantCulture) < 1000000000);
            Assert.Contains("Native lifecycle", await client.GetStringAsync("/instances/" + instance, token), StringComparison.Ordinal);
            var fields = Hidden(await client.GetStringAsync("/start/sample.leave-request.leave-request/" + releasedVersion, token));
            fields["field.startDate"] = "2026-11-02"; fields["field.endDate"] = "2026-11-04"; fields["field.leaveType"] = "paid"; fields["field.reason"] = "Packaged release";
            using var response = await client.PostAsync("/workflow/start/sample.leave-request.leave-request/" + releasedVersion, new FormUrlEncodedContent(fields), token); response.EnsureSuccessStatusCode();
            releaseInstance = Regex.Match(response.RequestMessage!.RequestUri!.Query, "started=([a-fA-F0-9-]+)").Groups[1].Value; Assert.True(Guid.TryParse(releaseInstance, out _));
        }
        finally { await Stop(released); }
        var forbidden = await RunCli("verify", Path.Combine(module, ".ew", "environments", "first", "artifacts", "g1"));
        Assert.NotEqual(0, forbidden.Code); Assert.Contains("EW7051", forbidden.Text, StringComparison.Ordinal);
        Directory.Move(packageOutput, Path.Combine(root, "external-packages-moved"));
        var afterMove = await Start("first");
        try { await Login(); Assert.Contains("Packaged release", await client.GetStringAsync("/instances/" + releaseInstance, token), StringComparison.Ordinal); }
        finally { await Stop(afterMove); }
        var required = Path.Combine(module, ".ew", "environments", "first", "artifacts", "g1"); var held = Path.Combine(root, "held-g1"); Directory.Move(required, held);
        try
        {
            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentArtifacts.RequiredAsync(Path.Combine(module, ".ew", "environments", "first"), Path.Combine(module, ".ew", "environments", "first", "workflow.db"), Path.Combine(module, ".ew", "environments", "first", "artifacts", "g2"), [], true));
            Assert.Contains(instance.Replace("-", "", StringComparison.Ordinal), missing.Message, StringComparison.Ordinal);
        }
        finally { Directory.Move(held, required); }
        // Keep the disposable environments as evidence for this opt-in run.
        Console.WriteLine("Native lifecycle evidence: " + root);

        async Task Login()
        {
            var html = await client.GetStringAsync("/__ew/dev", token); var fields = Hidden(html); fields["identity"] = "ew-dev-alice";
            using var response = await client.PostAsync("/__ew/dev/identity", new FormUrlEncodedContent(fields), token); response.EnsureSuccessStatusCode();
        }
        async Task<string> Version()
        {
            var html = await client.GetStringAsync("/start", token); var match = StartLink().Match(html); Assert.True(match.Success, html); return match.Groups[1].Value;
        }
        async Task<(Process Process, Task Output, Task Error)> Start(string environment, string? artifactPath = null, bool reset = false)
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { cli, "dev", "--environment", environment, "--url", url }) start.ArgumentList.Add(argument);
            foreach (var argument in artifactPath is null ? new[] { project, "--configuration", "Release", "--no-watch" } : new[] { "--artifact", artifactPath, "--config", Path.Combine(module, "ew.dev.json") }) start.ArgumentList.Add(argument);
            if (reset) start.ArgumentList.Add("--reset");
            var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(token); var stderr = process.StandardError.ReadToEndAsync(token);
            var result = (process, (Task)stdout, (Task)stderr);
            for (var attempt = 0; attempt < 180; attempt++)
            {
                if (process.HasExited) { Assert.Fail(await stdout + await stderr); }
                try { using var ready = await client.GetAsync("/__ew/dev", token); if (ready.IsSuccessStatusCode) return result; }
                catch (HttpRequestException) { }
                await Task.Delay(250, token);
            }
            await Stop(result); Assert.Fail("Development host readiness timeout"); return result;
        }
        async Task<(int Code, string Text)> RunCli(params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(cli); foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(token); var error = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token); return (process.ExitCode, await output + await error);
        }
        async Task Stop((Process Process, Task Output, Task Error) state)
        {
            var process = state.Process;
            if (!process.HasExited) { await process.StandardInput.WriteLineAsync("stop"); await process.StandardInput.FlushAsync(token); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(token); } }
            await Task.WhenAll(state.Output, state.Error); process.Dispose();
        }
    }
    private static Dictionary<string, string> Hidden(string html) => HiddenInput().Matches(html).GroupBy(match => WebUtility.HtmlDecode(match.Groups[1].Value), StringComparer.Ordinal).ToDictionary(group => group.Key, group => WebUtility.HtmlDecode(group.Last().Groups[2].Value), StringComparer.Ordinal);
    [GeneratedRegex("<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"[^>]*>")]
    private static partial Regex HiddenInput();
    [GeneratedRegex("href=\"/start/sample\\.leave-request\\.leave-request/(\\d+)\"")]
    private static partial Regex StartLink();
    [GeneratedRegex("href=\"/tasks/([a-fA-F0-9-]+)\"")]
    private static partial Regex TaskLink();
}

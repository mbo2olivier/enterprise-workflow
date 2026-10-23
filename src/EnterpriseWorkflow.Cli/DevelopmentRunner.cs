using System.Diagnostics;
using System.Text.Json;

internal static class DevelopmentRunner
{
    internal static async Task RunAsync(string project, string? configFile, string? url, bool noWatch, string configuration, bool reset, string[] additionalModules)
    {
        var config = await DevelopmentConfiguration.ReadAsync(project, configFile, url, noWatch);
        config.ModuleDirectories = config.ModuleDirectories.Concat(additionalModules).Distinct(StringComparer.Ordinal).ToArray();
        var projectRoot = Path.GetDirectoryName(project)!;
        var stateRoot = Path.Combine(projectRoot, ".ew", "dev");
        Directory.CreateDirectory(Path.GetDirectoryName(stateRoot)!);
        if (new DirectoryInfo(Path.Combine(projectRoot, ".ew")).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new ArgumentException("The development workspace must not be a symbolic link.");
        // Hold an OS-level exclusive handle for the entire supervisor lifetime.
        using var sessionLock = new FileStream(Path.Combine(projectRoot, ".ew", "dev.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (reset && Directory.Exists(stateRoot))
        {
            var relativeDatabase = Path.GetRelativePath(stateRoot, config.Storage.Path);
            if (relativeDatabase.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeDatabase) ||
                new DirectoryInfo(stateRoot).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("Reset is limited to an owned .ew/dev directory with its database inside it.");
            var archive = Path.Combine(projectRoot, ".ew", "archives", DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            Directory.Move(stateRoot, archive);
            Console.WriteLine($"Ancien environnement conservé : {archive}");
        }
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(config.Storage.Path)!);
        var artifacts = Path.Combine(stateRoot, "artifacts");
        Directory.CreateDirectory(artifacts);
        var runtimeConfig = Path.Combine(stateRoot, "runtime.json");
        await File.WriteAllTextAsync(runtimeConfig, JsonSerializer.Serialize(config));
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += handler;
        if (Console.IsInputRedirected)
            _ = Task.Run(async () => { if (await Console.In.ReadLineAsync() == "stop") await stop.CancelAsync(); });
        Process? host = null;
        try
        {
            host = await RebuildAsync();
            var snapshot = Snapshot(projectRoot);
            if (!config.Watch)
            {
                await host.WaitForExitAsync(stop.Token);
                if (host.ExitCode != 0) throw new InvalidOperationException($"Development host failed (exit {host.ExitCode}).");
                return;
            }
            Console.WriteLine("Surveillance active. Identités : /__ew/dev — Ctrl+C pour arrêter.");
            while (!stop.IsCancellationRequested)
            {
                await Task.Delay(750, stop.Token);
                if (host.HasExited) throw new InvalidOperationException($"Development host stopped (exit {host.ExitCode}).");
                var next = Snapshot(projectRoot);
                if (snapshot == next) continue;
                snapshot = next;
                try { host = await RebuildAsync(); }
                catch (Exception exception) when (exception is InvalidOperationException or IOException)
                {
                    Console.Error.WriteLine(exception.Message);
                    Console.Error.WriteLine(host.HasExited ? "Runtime arrêté." : "Ancienne génération toujours active.");
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally
        {
            Console.CancelKeyPress -= handler;
            Stop(host);
            host?.Dispose();
        }

        async Task<Process> RebuildAsync()
        {
            var staging = Path.Combine(stateRoot, "build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                await WorkflowCli.BuildArtifactAsync(project, staging, configuration, prepareOnly: true);
                using var preview = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(staging, "resolved-module.json")));
                var candidate = preview.RootElement.GetProperty("modules")[0];
                var id = candidate.GetProperty("id").GetString()!;
                var release = candidate.GetProperty("release").GetString()!;
                if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || release.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    Path.IsPathRooted(id) || Path.IsPathRooted(release) ||
                    id.Contains('/') || id.Contains('\\') || release.Contains('/') || release.Contains('\\') || id is "." or ".." || release is "." or "..")
                    throw new InvalidOperationException("Invalid artifact directory name.");
                var destination = Path.Combine(artifacts, id, release);
                var previousDirectories = Directory.GetFiles(artifacts, "module.json", SearchOption.AllDirectories).Select(path => Path.GetDirectoryName(path)!).Concat(config.ModuleDirectories).Distinct(StringComparer.Ordinal).ToArray();
                if (Directory.Exists(destination))
                {
                    using var old = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(destination, "resolved-module.json")));
                    if (old.RootElement.GetProperty("modules")[0].GetProperty("sha256").GetString() != candidate.GetProperty("sha256").GetString())
                        throw new InvalidOperationException("Release already used with another hash. Change the module release (and contract revision when necessary), or explicitly reset the local environment.");
                }
                else
                {
                    await WorkflowCli.RunDotnetAsync(new[] { typeof(DevelopmentRunner).Assembly.Location, "__verify-set" }.Concat(previousDirectories).Append(staging).ToArray());
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    Directory.Move(staging, destination);
                }
                var modules = Directory.GetFiles(artifacts, "module.json", SearchOption.AllDirectories).Select(path => Path.GetDirectoryName(path)!)
                    .Concat(config.ModuleDirectories).Distinct(StringComparer.Ordinal).ToArray();
                await WorkflowCli.RunDotnetAsync(new[] { typeof(DevelopmentRunner).Assembly.Location, "__verify-set" }.Concat(modules).ToArray());
                Stop(host);
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true };
                foreach (var argument in new[] { typeof(DevelopmentRunner).Assembly.Location, "__host", runtimeConfig, stateRoot }.Concat(modules!))
                    start.ArgumentList.Add(argument!);
                var nextHost = Process.Start(start) ?? throw new InvalidOperationException("Could not start development host.");
                host?.Dispose();
                return nextHost;
            }
            finally
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                catch (IOException) when (OperatingSystem.IsWindows()) { }
                catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows()) { }
            }
        }
    }

    private static string Snapshot(string root)
    {
        var entries = new List<string>();
        try
        {
            foreach (var file in Enumerate(root).Order(StringComparer.Ordinal))
            {
                try { entries.Add($"{file}:{File.GetLastWriteTimeUtc(file).Ticks}:{new FileInfo(file).Length}"); }
                catch (FileNotFoundException) { } // An editor can atomically replace a file during a scan.
            }
        }
        catch (DirectoryNotFoundException) { } // A directory rename can overlap a scan; the next scan retries it.
        return string.Join('\n', entries);
    }
    private static IEnumerable<string> Enumerate(string directory)
    {
        foreach (var file in Directory.GetFiles(directory))
            if (Path.GetExtension(file) is ".cs" or ".razor" or ".css" or ".js" or ".csproj" or ".props" or ".targets") yield return file;
        foreach (var child in Directory.GetDirectories(directory))
            if (Path.GetFileName(child) is not ("bin" or "obj" or ".ew" or ".git" or "artifacts" or "node_modules") &&
                !new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint) && !File.Exists(Path.Combine(child, "module.json")))
                foreach (var file in Enumerate(child)) yield return file;
    }
    private static void Stop(Process? process)
    {
        if (process is null) return;
        if (!process.HasExited)
        {
            process.StandardInput.WriteLine("stop");
            process.StandardInput.Flush();
            if (!process.WaitForExit(10000)) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        }
    }
}

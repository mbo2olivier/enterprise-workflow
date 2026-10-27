using EnterpriseWorkflow.Extensions;
using System.Diagnostics;
using System.Text.Json;

internal static class DevelopmentRunner
{
    internal static async Task RunAsync(string project, string? configFile, string? url, bool noWatch, string? configuration, bool reset, string[] additionalModules, string? environment = null, string? artifact = null)
    {
        var sourceMode = artifact is null;
        var root = sourceMode ? Path.GetDirectoryName(project)! : configFile is null ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(Path.GetFullPath(configFile))!;
        var config = await DevelopmentConfiguration.ReadAsync(sourceMode ? project : Path.Combine(root, "artifact.csproj"), configFile, url, noWatch, environment, configuration);
        if (!sourceMode) config.Watch = false;
        config.ModuleDirectories = config.ModuleDirectories.Concat(additionalModules).Distinct(StringComparer.Ordinal).ToArray();
        var stateRoot = config.StateRoot;
        DevelopmentArtifacts.EnsureOwnedPath(stateRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(stateRoot)!);
        using var sessionLock = new FileStream(stateRoot + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (reset && Directory.Exists(stateRoot))
        {
            var relativeDatabase = Path.GetRelativePath(stateRoot, config.Storage.Path!);
            if (relativeDatabase.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeDatabase))
                throw new ArgumentException("Reset is limited to an owned environment with its database inside it.");
            var archive = Path.Combine(root, ".ew", "archives", config.EnvironmentName + "-" + Guid.NewGuid().ToString("N"));
            DevelopmentArtifacts.EnsureOwnedPath(archive);
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!); Directory.Move(stateRoot, archive);
            Console.WriteLine($"Ancien environnement conservé : {archive}");
        }
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(config.Storage.Path!)!);
        var artifacts = Path.Combine(stateRoot, "artifacts"); Directory.CreateDirectory(artifacts);
        var runtimeConfig = Path.Combine(stateRoot, "runtime.json");
        await File.WriteAllTextAsync(runtimeConfig, JsonSerializer.Serialize(config));
        Console.WriteLine($"Environnement : {config.EnvironmentName} — {stateRoot}");
        if (Directory.Exists(Path.Combine(root, ".ew", "dev"))) Console.WriteLine("Ancien espace .ew/dev conservé ; il n’est ni importé ni réinitialisé automatiquement.");
        var projectRoot = root;
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
            string primary;
            string? staging = null;
            try
            {
                if (sourceMode)
                {
                    var generations = DevelopmentArtifacts.ReadGenerations(stateRoot);
                    var occupied = Directory.GetDirectories(artifacts).Select(Path.GetFileName).Where(name => name?.StartsWith('g') == true)
                        .Select(name => int.TryParse(name.AsSpan(1), out var number) ? number : 0).DefaultIfEmpty().Max();
                    var next = checked(Math.Max(generations.Select(item => item.Number).DefaultIfEmpty().Max(), occupied) + 1);
                    staging = Path.Combine(stateRoot, "build-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
                    await WorkflowCli.BuildArtifactAsync(project, staging, config.Build.Configuration, config.ModuleDirectories, prepareOnly: true, developmentGeneration: next);
                    var fingerprint = DevelopmentArtifacts.Fingerprint(staging);
                    var existing = generations.SingleOrDefault(item => item.Fingerprint == fingerprint);
                    if (existing is not null)
                    {
                        primary = Path.Combine(artifacts, "g" + existing.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        if (DevelopmentArtifacts.Fingerprint(primary) != fingerprint) throw new InvalidOperationException("The generation registry does not match its immutable artifact.");
                    }
                    else
                    {
                        primary = Path.Combine(artifacts, "g" + next.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        await WorkflowCli.RunDotnetAsync(new[] { typeof(DevelopmentRunner).Assembly.Location, "__verify-dev-set" }.Concat(config.ModuleDirectories).Append(staging).ToArray());
                        Directory.Move(staging, primary);
                        generations.Add(new(next, fingerprint));
                        var index = Path.Combine(stateRoot, "generations.json");
                        await File.WriteAllTextAsync(index + ".tmp", JsonSerializer.Serialize(generations)); File.Move(index + ".tmp", index, true);
                    }
                }
                else
                {
                    primary = Path.GetFullPath(artifact!);
                    WorkflowModuleCatalog.InspectArtifact(primary);
                }
                if (!sourceMode) primary = DevelopmentArtifacts.CacheArtifact(stateRoot, primary, allowDevelopment: false);
                var dependencies = config.ModuleDirectories.Select(path => DevelopmentArtifacts.CacheArtifact(stateRoot, path, allowDevelopment: true)).ToArray();
                var modules = await DevelopmentArtifacts.RequiredAsync(stateRoot, config.Storage.Path!, primary, dependencies, allowDevelopment: true);
                try { await WorkflowCli.RunDotnetAsync(new[] { typeof(DevelopmentRunner).Assembly.Location, "__verify-dev-set" }.Concat(modules).ToArray()); }
                catch (InvalidOperationException exception) { var requirements = await DevelopmentArtifacts.DescribeRequirementsAsync(config.Storage.Path!); throw new InvalidOperationException($"Environment {config.EnvironmentName}: required artifacts cannot be loaded ({string.Join(", ", modules)}). Instances: {requirements}. {exception.Message}", exception); }
                Stop(host);
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true };
                foreach (var argument in new[] { typeof(DevelopmentRunner).Assembly.Location, "__host-session", runtimeConfig, stateRoot, bool.TrueString, primary }.Concat(modules)) start.ArgumentList.Add(argument);
                var nextHost = Process.Start(start) ?? throw new InvalidOperationException("Could not start development host.");
                host?.Dispose(); return nextHost;
            }
            finally
            {
                try { if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true); }
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

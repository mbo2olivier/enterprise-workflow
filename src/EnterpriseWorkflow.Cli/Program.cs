using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

// Keep redirected CLI diagnostics consistent across Windows and Unix consumers.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

try
{
    if (args.Length >= 4 && args[0] is "__prepare" or "__prepare-dev")
        return await WorkflowCli.PrepareAsync(args[1], args[2], args[3], args[(args[0] == "__prepare-dev" ? 5 : 4)..], args[0] == "__prepare-dev", args[0] == "__prepare-dev" && int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) is > 0 and var generation ? generation : null);
    if (args.Length >= 2 && args[0] is "__verify-set" or "__verify-dev-set")
    {
        await WorkflowCli.VerifySetAsync(args[1..], allowDevelopment: args[0] == "__verify-dev-set");
        return 0;
    }
    if (args.Length >= 6 && args[0] == "__host-session")
    {
        var hostConfig = JsonSerializer.Deserialize<DevelopmentConfiguration>(await File.ReadAllTextAsync(args[1]))!;
        await DevelopmentHost.RunAsync(hostConfig, args[5..], args[2], args[4], bool.Parse(args[3]));
        return 0;
    }
    if (args.Length >= 4 && args[0] == "__host")
    {
        var hostConfig = JsonSerializer.Deserialize<DevelopmentConfiguration>(await File.ReadAllTextAsync(args[1]))!;
        await DevelopmentHost.RunAsync(hostConfig, args[3..], args[2]);
        return 0;
    }
    return await WorkflowCli.RunAsync(args);
}
catch (MemberAccessException exception)
{
    Console.Error.WriteLine("EW7050_INCOMPATIBLE_MODULE_API: Un artefact requis a été compilé avec une autre API du SDK. " +
        "Utilisez un environnement neuf avec ew dev --environment <nom>, ou un runtime compatible pour reprendre les instances conservées. " +
        "Aucun artefact n'a été remplacé. " + exception.Message);
    return 1;
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or JsonException or ReflectionTypeLoadException or System.Security.VerificationException or NotSupportedException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

internal static class WorkflowCli
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                Console.WriteLine("ew validate|inspect|pack <project-or-directory> [--configuration Debug|Release] [--output path]\new dev [project | --artifact directory] [--environment name] [--config file] [--url http://localhost:5080] [--no-watch] [--reset]\new verify <artifact-directory>\new inspect <project> --format json");
                return 0;
            }
            if (args[0] == "--version")
            {
                Console.WriteLine(typeof(WorkflowCli).Assembly.GetName().Version);
                return 0;
            }
            var command = args[0];
            if (command is not ("validate" or "inspect" or "pack" or "verify" or "dev"))
                throw new ArgumentException($"Unknown command '{command}'. Use ew --help.");
            var options = Parse(args[1..]);
            if (command != "dev" && (options.Config is not null || options.Url is not null || options.NoWatch || options.Reset || options.Environment is not null || options.Artifact is not null))
                throw new ArgumentException("--config, --url, --no-watch and --reset are development-only options.");
            if (command is "verify" or "validate" && options.Output is not null)
                throw new ArgumentException("--output is supported by inspect and pack only.");
            if (command == "verify")
            {
                await VerifySetAsync(options.ModuleDirectories.Append(Path.GetFullPath(options.Project)).ToArray());
                Console.WriteLine("Artifact verified.");
                return 0;
            }
            if (command == "dev")
            {
                if (options.Artifact is not null && (options.HasProject || options.Configuration is not null || options.NoWatch))
                    throw new ArgumentException("--artifact cannot be combined with a source project, --configuration or --no-watch.");
                await DevelopmentRunner.RunAsync(options.Artifact is null ? ResolveProject(options.Project) : string.Empty, options.Config, options.Url, options.NoWatch, options.Configuration, options.Reset, options.ModuleDirectories, options.Environment, options.Artifact);
                return 0;
            }
            var project = ResolveProject(options.Project);
            var staging = Path.Combine(Path.GetDirectoryName(project)!, ".ew", "build", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            // An isolated child prepares and validates the package. Its exit releases DLLs on Windows.
            try
            {
                await BuildArtifactAsync(project, staging, options.Configuration ?? "Debug", options.ModuleDirectories);
                if (command == "pack")
                {
                    using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(staging, "module.json")));
                    var id = manifest.RootElement.GetProperty("id").GetString()!;
                    var version = manifest.RootElement.GetProperty("version").GetString()!;
                    if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        id is "." or ".." || version is "." or ".." || id.Contains('/') || version.Contains('/') || id.Contains('\\') || version.Contains('\\'))
                        throw new InvalidOperationException("Module ID/release cannot be used as an artifact directory name.");
                    var destination = Path.GetFullPath(Path.Combine(options.Output ?? "artifacts", id, version));
                    if (Directory.Exists(destination))
                        throw new IOException($"Artifact already exists: {destination}. Choose another output or release; artifacts are immutable.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    Directory.Move(staging, destination);
                    Console.WriteLine(destination);
                }
                else if (command == "inspect")
                {
                    var preview = await File.ReadAllTextAsync(Path.Combine(staging, "resolved-module.json"));
                    if (!options.Json) preview = RenderPreview(preview);
                    if (options.Output is not null)
                    {
                        var output = Path.GetFullPath(options.Output);
                        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                        await File.WriteAllTextAsync(output, preview);
                        Console.WriteLine(output);
                    }
                    else Console.WriteLine(preview);
                }
                else Console.WriteLine("Module validation succeeded.");
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            }
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or JsonException or ReflectionTypeLoadException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static Options Parse(string[] args)
    {
        string? project = null, output = null, config = null, url = null, environment = null, artifact = null;
        var noWatch = false;
        var reset = false;
        var json = false;
        string? configuration = null;
        var moduleDirectories = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument is "--configuration" or "--output" or "--project" or "--format" or "--config" or "--url" or "--module-directory" or "--environment" or "--artifact")
            {
                if (++index >= args.Length) throw new ArgumentException($"Missing value for {argument}.");
                switch (argument)
                {
                    case "--environment": environment = args[index]; break;
                    case "--artifact": artifact = Path.GetFullPath(args[index]); break;
                    case "--module-directory": moduleDirectories.Add(Path.GetFullPath(args[index])); break;
                    case "--config": config = args[index]; break;
                    case "--url": url = args[index]; break;
                    case "--configuration": configuration = args[index]; break;
                    case "--output": output = args[index]; break;
                    case "--project":
                        if (project is not null) throw new ArgumentException("Project specified more than once.");
                        project = args[index]; break;
                    case "--format":
                        if (args[index] is not ("json" or "text")) throw new ArgumentException("Supported inspection formats: text, json.");
                        json = args[index] == "json";
                        break;
                }
            }
            else if (argument == "--reset") reset = true;
            else if (argument == "--no-watch") noWatch = true;
            else if (argument.StartsWith('-')) throw new ArgumentException($"Unknown option '{argument}'.");
            else if (project is not null) throw new ArgumentException("Only one project/directory is supported.");
            else project = argument;
        }
        if (configuration is not (null or "Debug" or "Release")) throw new ArgumentException("Configuration must be Debug or Release.");
        return new(project ?? ".", configuration, output, config, url, noWatch, reset, json, moduleDirectories.ToArray(), environment, artifact, project is not null);
    }

    private static string RenderPreview(string preview)
    {
        using var document = JsonDocument.Parse(preview);
        var text = new System.Text.StringBuilder();
        foreach (var module in document.RootElement.GetProperty("modules").EnumerateArray())
            text.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Module : {module.GetProperty("id").GetString()} / {module.GetProperty("release").GetString()} / {module.GetProperty("sha256").GetString()}");
        foreach (var process in document.RootElement.GetProperty("processes").EnumerateArray())
        {
            text.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Processus : {process.GetProperty("id").GetString()} / révision {process.GetProperty("version").GetInt32()}");
            foreach (var node in process.GetProperty("nodes").EnumerateArray())
                text.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Étape {node.GetProperty("id").GetString()}: {node.GetRawText()}");
            foreach (var edge in process.GetProperty("transitions").EnumerateArray())
                text.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Transition : {edge.GetRawText()}");
        }
        return text.ToString();
    }

    private static string ResolveProject(string input)
    {
        var path = Path.GetFullPath(input);
        if (File.Exists(path) && Path.GetExtension(path) == ".csproj") return path;
        if (!Directory.Exists(path)) throw new ArgumentException($"Project/directory not found: {path}");
        var projects = Directory.GetFiles(path, "*.csproj");
        return projects.Length == 1 ? projects[0] : throw new ArgumentException("Expected exactly one project. Use --project <file.csproj>.");
    }

    internal static async Task BuildArtifactAsync(string project, string staging, string configuration, string[]? moduleDirectories = null, bool prepareOnly = false, int? developmentGeneration = null)
    {
        var entryFile = await ReadTargetFileNameAsync(project, configuration);
        var output = Path.Combine(staging, "publish");
        await RunDotnetAsync(["publish", project, "--configuration", configuration, "--output", output,
            "--nologo", "-p:UseSharedCompilation=false", "-p:GenerateDependencyFile=false", "-m:1"]);
        var prepareArguments = new List<string> { typeof(WorkflowCli).Assembly.Location, prepareOnly ? "__prepare-dev" : "__prepare", output, staging, entryFile };
        if (prepareOnly) prepareArguments.Add((developmentGeneration ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        await RunDotnetAsync(prepareArguments.Concat(moduleDirectories ?? []).ToArray());
        Directory.Delete(output, recursive: true);
    }

    internal static async Task<int> PrepareAsync(string output, string staging, string entryFile, string[] moduleDirectories, bool prepareOnly, int? developmentGeneration = null)
    {
        // Resolve dependencies from the publish directory, retaining shared framework contracts.
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(output, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        if (Directory.Exists(Path.Combine(output, "runtimes")) ||
            Directory.GetFiles(output, "*", SearchOption.AllDirectories).Any(path =>
                (Path.GetExtension(path) is ".so" or ".dylib") ||
                (Path.GetExtension(path) == ".dll" && Path.GetDirectoryName(path) != output && !Path.GetRelativePath(output, path).StartsWith("wwwroot" + Path.DirectorySeparatorChar, StringComparison.Ordinal))))
            throw new NotSupportedException("Native/RID-specific or satellite module dependencies are not supported by this packaging format. No incomplete artifact was produced.");
        var candidates = new List<(string Path, Type Type)>();
        if (Path.GetFileName(entryFile) != entryFile || !entryFile.EndsWith(".dll", StringComparison.Ordinal))
            throw new InvalidOperationException("Project must produce a managed DLL.");
        foreach (var path in new[] { Path.Combine(output, entryFile) })
        {
            var name = AssemblyName.GetAssemblyName(path);
            var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(item => item.GetName().Name == name.Name)
                ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            candidates.AddRange(assembly.GetExportedTypes()
                .Where(type => !type.IsAbstract && typeof(WorkflowModule).IsAssignableFrom(type))
                .Select(type => (path, type)));
        }
        if (candidates.Count != 1) throw new InvalidOperationException($"Expected exactly one fluent WorkflowModule entry point; found {candidates.Count}.");
        var candidate = candidates[0];
        var module = (WorkflowModule)Activator.CreateInstance(candidate.Type)!;
        var assemblyPath = Path.Combine(staging, Path.GetFileName(candidate.Path));
        File.Copy(candidate.Path, assemblyPath);
        var dependencies = new List<string>();
        foreach (var path in Directory.GetFiles(output, "*.dll").Where(path => path != candidate.Path).Order(StringComparer.Ordinal))
        {
            var target = Path.Combine(staging, "dependencies", Path.GetFileName(path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target);
            dependencies.Add(target);
        }
        var resources = new List<(string Path, string ContentType)>();
        var webroot = Path.Combine(output, "wwwroot");
        if (Directory.Exists(webroot))
        {
            foreach (var path in Directory.GetFiles(webroot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var target = Path.Combine(staging, "resources", Path.GetRelativePath(webroot, path));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(path, target);
                var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                if (!contentTypes.TryGetContentType(path, out var contentType)) contentType = "application/octet-stream";
                resources.Add((target, contentType));
            }
        }
        await File.WriteAllTextAsync(Path.Combine(staging, "module.json"),
            WorkflowModuleManifestWriter.Create(module, assemblyPath, dependencies, resources, developmentGeneration));
        if (prepareOnly)
        {
            var metadata = WorkflowModuleCatalog.Load([staging], options => options.AllowDevelopmentGenerations = developmentGeneration is not null);
            await File.WriteAllTextAsync(Path.Combine(staging, "resolved-module.json"), JsonSerializer.Serialize(new
            {
                modules = metadata.Modules.Select(item => new { id = item.Manifest.Id.Value, release = item.Manifest.Version, sha256 = item.ArtifactSha256 }),
            }, JsonOptions));
        }
        else await VerifySetAsync(moduleDirectories.Append(staging).ToArray(), staging);
        return 0;
    }

    internal static async Task VerifySetAsync(string[] directories, string? previewDirectory = null, bool allowDevelopment = false)
    {
        var catalog = WorkflowModuleCatalog.Load(directories, options => options.AllowDevelopmentGenerations = allowDevelopment);
        var services = new ServiceCollection();
        services.AddEnterpriseWorkflowModules(catalog);
        await using var provider = services.BuildServiceProvider();
        var presentation = provider.GetRequiredService<IWorkflowPresentationCatalog>();
        var registry = provider.GetRequiredService<IWorkflowHandlerRegistry>();
        foreach (var process in presentation.Processes)
            WorkflowDefinitionBindingValidator.Validate(process.Definition, registry).EnsureValid();
        var preview = JsonSerializer.Serialize(new
        {
            modules = catalog.Modules.Select(module => new { id = module.Manifest.Id.Value, release = module.Manifest.Version, sha256 = module.ArtifactSha256 }),
            permissions = presentation.Permissions.Select(permission => new { id = permission.Id.Value, label = permission.Label }),
            processes = presentation.Processes.Select(process => JsonSerializer.Deserialize<JsonElement>(process.Definition.CanonicalJson)),
        }, JsonOptions);
        // Verification is read-only. Preview is generated only in the preparation child.
        if (previewDirectory is not null)
            await File.WriteAllTextAsync(Path.Combine(previewDirectory, "resolved-module.json"), preview);
    }

    private static async Task<string> ReadTargetFileNameAsync(string project, string configuration)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "msbuild", project, "-nologo", "-getProperty:TargetFileName", $"-property:Configuration={configuration}" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not evaluate project.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var name = (await output).Trim();
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Project evaluation failed: " + await error);
        return name;
    }

    internal static async Task RunDotnetAsync(string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardError());
        var stderr = process.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError());
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync());
        if (process.ExitCode != 0) throw new InvalidOperationException($"dotnet subprocess failed (exit {process.ExitCode}).");
    }

    private sealed record Options(string Project, string? Configuration, string? Output, string? Config, string? Url, bool NoWatch, bool Reset, bool Json, string[] ModuleDirectories, string? Environment, string? Artifact, bool HasProject);
}

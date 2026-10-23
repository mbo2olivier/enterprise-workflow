using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Persistence.Sqlite;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Security.Persistence;
using EnterpriseWorkflow.Security.Persistence.Sqlite;
using EnterpriseWorkflow.Web;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.DataProtection;

internal static class DevelopmentHost
{
    internal static async Task RunAsync(DevelopmentConfiguration configuration, string[] moduleDirectories, string stateRoot)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = "Development",
            ApplicationName = typeof(DevelopmentHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuration.ToHostConfiguration());
        builder.WebHost.UseUrls(configuration.Server.Url);
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(stateRoot, "keys")));
        Microsoft.AspNetCore.Hosting.StaticWebAssets.StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        var connection = new SqliteConnectionStringBuilder { DataSource = configuration.Storage.Path }.ToString();
        var workflowDatabase = new SqliteWorkflowDatabase(connection);
        var securityDatabase = new SqliteSecurityDatabase(connection);
        await workflowDatabase.MigrateAsync(CancellationToken.None);
        await securityDatabase.MigrateAsync(CancellationToken.None);
        var workflowStore = new SqliteWorkflowStore(workflowDatabase);
        var policy = builder.Configuration.GetSection("Security:Policy").Get<SecurityPolicyOptions>() ?? new();
        policy.Validate();
        var timeProvider = TimeProvider.System;
        var securityStore = securityDatabase.CreateStore(timeProvider);
        var localAuthentication = new LocalAuthenticationProvider(securityStore, policy, timeProvider);
        var moduleCatalog = WorkflowModuleCatalog.Load(moduleDirectories);
        await WorkflowModuleKernel.ReconcileAsync(moduleCatalog, workflowStore);
        var configuredDirectories = new Dictionary<string, IIdentityDirectory>(StringComparer.Ordinal);
        IAuthenticationProvider selectedAuthentication = localAuthentication;
        IReadOnlyDictionary<string, IIdentityDirectory> directories = configuredDirectories;
        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(policy);
        builder.Services.AddSingleton(workflowStore);
        builder.Services.AddSingleton<IWorkflowStore>(workflowStore);
        builder.Services.AddSingleton<IWorkflowMaintenanceStore>(workflowStore);
        builder.Services.AddSingleton(securityStore);
        builder.Services.AddSingleton<ISecurityProfileStore>(securityStore);
        builder.Services.AddSingleton<ILocalAccountStore>(securityStore);
        builder.Services.AddSingleton<ISecuritySessionStore>(securityStore);
        builder.Services.AddSingleton<ILocalSecurityProvisioningStore>(securityStore);
        builder.Services.AddSingleton<IWorkflowAccessStore>(securityStore);
        builder.Services.AddSingleton<IPresentationSettingsStore>(securityStore);
        builder.Services.AddSingleton(directories);
        builder.Services.AddSingleton(localAuthentication);
        builder.Services.AddSingleton<IAuthenticationProvider>(selectedAuthentication);
        builder.Services.AddSingleton<SecuritySessionManager>();
        builder.Services.AddSingleton<IAuthorizationProvider>(services =>
            new InternalProfileAuthorizationProvider(services.GetRequiredService<ISecurityProfileStore>(), directories));
        builder.Services.AddSingleton<IWorkflowAuthorizationService>(services =>
            new ModulePermissionAuthorizationService(services.GetRequiredService<IWorkflowPresentationCatalog>(),
                services.GetRequiredService<IAuthorizationProvider>(),
                new InternalWorkflowAuthorizationService(services.GetRequiredService<IWorkflowAccessStore>(), directories)));
        builder.Services.AddSingleton<IWorkflowActionCatalog>(services =>
        {
            var presentation = services.GetRequiredService<IWorkflowPresentationCatalog>();
            var descriptors = presentation.Processes.Where(process => WorkflowStagePolicy.Read(
                process.Definition.Nodes.Single(node => node.Role == EnterpriseWorkflow.Core.Model.WorkflowNodeRole.Start).Configuration) is null).SelectMany(process =>
            {
                var global = new[]
                {
                    new WorkflowActionDescriptor(new(process.Definition.Id.Value, process.Definition.Version, null, WorkflowActions.Start)),
                    new WorkflowActionDescriptor(new(process.Definition.Id.Value, process.Definition.Version, null, WorkflowActions.ReadInstance)),
                    new WorkflowActionDescriptor(new(process.Definition.Id.Value, process.Definition.Version, null, WorkflowActions.CancelInstance)),
                };
                var tasks = process.Definition.Nodes.Where(node => node.HumanTask is not null).SelectMany(node =>
                    node.HumanTask!.Actions.Select(action => new WorkflowActionDescriptor(new(
                        process.Definition.Id.Value, process.Definition.Version, node.Id.Value,
                        new WorkflowActionId(action.ActionId.Value))))
                    .Append(new(new(process.Definition.Id.Value, process.Definition.Version, node.Id.Value, WorkflowActions.ReadTask)))
                    .Append(new(new(process.Definition.Id.Value, process.Definition.Version, node.Id.Value, WorkflowActions.ClaimTask)))
                    .Append(new(new(process.Definition.Id.Value, process.Definition.Version, node.Id.Value, WorkflowActions.AssignTask))));
                return global.Concat(tasks);
            });
            return new WorkflowActionCatalog(descriptors);
        });
        builder.Services.AddSingleton<WorkflowAccessAdministration>();
        builder.Services.AddSingleton<LocalAccountAdministration>();

        builder.Services.AddEnterpriseWorkflowModules(moduleCatalog);
        var smtp = builder.Configuration.GetSection("Notifications:Smtp").Get<WorkflowSmtpOptions>() ?? new();
        smtp.Validate();
        builder.Services.AddSingleton(smtp);
        builder.Services.AddSingleton<IExternalEffectTransport>(services => new WorkflowNotificationTransport(
            services.GetRequiredService<IWorkflowStore>(), services.GetRequiredService<IWorkflowAuthorizationService>(),
            services.GetRequiredService<ISecurityProfileStore>(), services.GetRequiredService<ILocalAccountStore>(),
            directories, smtp, new SmtpWorkflowMailSender(smtp), new DevelopmentExternalEffectTransport()));
        builder.Services.AddEnterpriseWorkflowRuntime<DevelopmentExternalEffectTransport>(options => options.NotifyHumanTaskPools = smtp.Enabled);
        builder.Services.AddEnterpriseWorkflowPresentation(options =>
            options.InstallationId = builder.Configuration["Installation:Id"] ?? "enterprise-workflow");
        builder.Services.AddEnterpriseWorkflowWeb();
        builder.Services.AddSingleton(new WorkflowDevelopmentEnvironment(string.Join(", ", moduleCatalog.Modules.Select(module =>
            $"{module.Manifest.Id.Value} / {module.Manifest.Version} / {module.ArtifactSha256[..12]}"))));

        var app = builder.Build();
        await DevelopmentSeed.ApplyAsync(app.Services, configuration, stateRoot);
        DevelopmentSeed.MapSelector(app, configuration, moduleCatalog);

        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapEnterpriseWorkflowModuleResources();
        app.MapEnterpriseWorkflowWeb();
        _ = Task.Run(async () =>
        {
            if (await Console.In.ReadLineAsync() == "stop") app.Lifetime.StopApplication();
        });
        await app.StartAsync();
        Console.WriteLine($"Interface : {configuration.Server.Url}");
        Console.WriteLine($"Stockage : {configuration.Storage.Path}");
        await app.WaitForShutdownAsync();

    }
}
internal sealed class DevelopmentExternalEffectTransport : IExternalEffectTransport
{
    public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(
        OutboxLease message,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ExternalEffectDeliveryResult.PermanentFailure("kernel.transport-not-configured"));
}


using EnterpriseWorkflow.Hosting;
using Example.Workflow;

var builder = EnterpriseWorkflowApplication.CreateKernelBuilder().AddWebPresentation()
    .ConfigureModules(ExampleWorkflowServices.ConfigureModules)
    .ConfigureBranding(options =>
    {
        options.Defaults = options.Defaults with { DisplayName = "Example Workflow", AccentColor = "#6D28D9", LogoResourcePath = "/example-logo.svg" };
        options.FaviconPath = "/example-logo.svg";
        options.StylesheetPath = "/example-brand.css";
        options.LogoAccessibleLabel = "Example Workflow";
        options.Canvas = "#F5F3FF";
        options.EditableFields = EnterpriseWorkflow.Security.WorkflowBrandingFields.TimeZone;
    });
builder.Services.AddExampleWorkflowServices(builder.Configuration);
if (args is ["migrate"]) { await builder.MigrateAsync(); return; }
if (args is ["bootstrap", var user, var password])
{
    Console.WriteLine((await builder.BootstrapAsync(user, password)).Outcome); return;
}
await using var app = await builder.BuildAsync();
app.MapEnterpriseWorkflow();
await app.RunAsync();

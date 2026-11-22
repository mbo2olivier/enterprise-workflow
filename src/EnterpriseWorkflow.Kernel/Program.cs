using EnterpriseWorkflow.Hosting;
using EnterpriseWorkflow.Security;

var builder = EnterpriseWorkflowApplication.CreateKernelBuilder().AddWebPresentation()
    .ConfigureBranding(options => options.EditableFields = WorkflowBrandingFields.All);
if (args is ["migrate"])
{
    await builder.MigrateAsync();
    return;
}
if (args is ["bootstrap", var userName, var password])
{
    var result = await builder.BootstrapAsync(userName, password);
    Console.WriteLine(result.Outcome is ProviderOutcome.Succeeded
        ? "Administrateur local initialisé."
        : $"Échec: {result.ErrorCode}");
    return;
}
await using var app = await builder.BuildAsync();
app.MapEnterpriseWorkflow();
await app.RunAsync();

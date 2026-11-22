using EnterpriseWorkflow.Cli;
using Example.Workflow;

var builder = EnterpriseWorkflowCli.CreateBuilder(args).UseCommandName("few-example")
    .ConfigureModules(ExampleWorkflowServices.ConfigureModules)
    .ConfigureDevelopmentServices((context, services) => services.AddExampleWorkflowServices(context.Configuration))
    .AddTemplates("Example.Workflow.Templates", "1.0.0-rc.1", "few-example-module")
    .AddCommand("distribution", "Print the exact distribution release.", async context =>
    {
        await context.Output.WriteLineAsync("Example.Workflow 1.0.0-rc.1");
        return 0;
    });
return await builder.Build().RunAsync();

using System.Net.Http.Json;
using EnterpriseWorkflow.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Example.Workflow;

public static class ExampleWorkflowServices
{
    public static IServiceCollection AddExampleWorkflowServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IApprovalPolicy, HttpApprovalPolicy>(client =>
            client.BaseAddress = new Uri(configuration["Example:PolicyUrl"] ?? "http://127.0.0.1:5099"));
        return services;
    }
    public static void ConfigureModules(WorkflowModuleLoadOptions options)
    {
        options.RuntimeId = "Example.Workflow";
        options.RuntimeVersion = "1.0.0-rc.1";
        options.RequireRuntimeDeclaration = true;
        options.SharedAssemblies.Add(typeof(IApprovalPolicy).Assembly);
    }
}

internal sealed class HttpApprovalPolicy(HttpClient client) : IApprovalPolicy
{
    public async Task<bool> CanApproveAsync(string reason, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<bool>("/policy?reason=" + Uri.EscapeDataString(reason), cancellationToken);
}

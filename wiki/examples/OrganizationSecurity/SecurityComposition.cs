using EnterpriseWorkflow.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Example.Workflow.Security;

public static class SecurityComposition
{
    // Call before BuildAsync, with your real gateway implementations.
    public static EnterpriseWorkflowApplicationBuilder AddOrganizationSecurity(
        this EnterpriseWorkflowApplicationBuilder builder,
        IOrganizationIdentityGateway identities,
        IOrganizationSecondFactorGateway secondFactor)
    {
        builder.Services.AddSingleton(identities);
        builder.Services.AddSingleton(secondFactor);
        return builder
            .AddAuthenticationProvider<OrganizationAuthentication>()
            .AddIdentityProvider<OrganizationIdentities>()
            .AddSecondFactorHandler<OrganizationSecondFactor>();
    }
}

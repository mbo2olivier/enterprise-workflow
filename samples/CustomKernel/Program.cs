using EnterpriseWorkflow.Samples.CustomKernel;
using EnterpriseWorkflow.Hosting;
using EnterpriseWorkflow.Security;

// Choose extensions in code; the configuration only toggles their use.
var builder = EnterpriseWorkflowApplication.CreateWebBuilder(args).AddWebPresentation();
builder.AddSecondFactorHandler<DemonstrationSecondFactorHandler>();
// To use a corporate source: builder.AddAuthenticationProvider<CorporateAuthentication>()
//     .AddIdentityProvider<CorporateIdentityProvider>();
// Those implementations must be supplied by the consuming organization.
await using var app = await builder.BuildAsync();
app.MapEnterpriseWorkflow();
await app.RunAsync();


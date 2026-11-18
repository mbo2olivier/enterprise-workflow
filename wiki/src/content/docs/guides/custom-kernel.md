---
title: Custom kernels and optional security capabilities
description: Compose an Enterprise Workflow host with compiled organization-specific services.
---

L11 introduces the `EnterpriseWorkflow.Hosting` SDK. The APIs below are under local qualification and are not included in the previously published `1.0.0-rc.1` packages. Build the repository to try them.

```csharp
using EnterpriseWorkflow.Hosting;

var builder = EnterpriseWorkflowApplication.CreateWebBuilder(args)
    .AddWebPresentation();

builder.AddAuthenticationProvider<CorporateAuthentication>();
builder.AddIdentityProvider<CorporateIdentities>();
builder.AddSecondFactorHandler<CorporateSecondFactor>();
builder.ConfigureRuntime(options => options.NotifyHumanTaskPools = true);
builder.Services.AddHttpClient<CorporateIdentityClient>();

await using var host = await builder.BuildAsync();
host.MapEnterpriseWorkflow();
host.WebApplication.MapGet("/company/health", () => "ready");
await host.RunAsync();
```

The corporate classes are supplied by the consuming application. The `samples/CustomKernel` example compiles without them and deliberately uses an unavailable second-factor demonstration handler. `CreateBuilder(args)` creates a non-web host. `CreateWebBuilder(args)` exposes `WebHost`; both expose `Configuration`, `Services`, `Logging` and `Environment`. `ConfigureModules`, `ConfigureHandlers`, `ConfigurePresentation` and `UseDatabase` customize composition. `UseExternalEffectTransport<T>` replaces the complete effect transport, including default notification dispatch: retain or compose notification handling explicitly if replacing it.

Database provider, connection string, module directories and options use the standard configuration keys. Schema validation is the default; `MigrateAsync()` is an explicit operation. `BootstrapAsync(userName, password)` provisions the local administrator. The shipped kernel preserves its `migrate` and `bootstrap` commands and local/LDAP providers.

## Optional identity capabilities

Implement only the necessary contracts in `EnterpriseWorkflow.Security`:

- `IAuthenticationProvider`: primary password authentication, independent of local authorization.
- `IIdentityResolver` and `IIdentitySearchProvider`: identity details and bounded searches.
- `IIdentityStatusProvider`: optional remote account status.
- `IIdentityAdmissionProvider`: local approval/activation state, checked for sessions and authorization.
- `IIdentityImportProvider`: resolve an identifier for local import without creating a remote account.
- `IIdentityProvisioningProvider`: optional maker/checker provisioning, including local admission.

Register a capability implementation with `AddIdentityProvider<T>()`. Individual resolution/search/status contracts are adapted to the existing directory consumers. Permissions and workflow access remain local. Identities are keyed by `(ProviderId, SubjectId)`; never use a mutable login as a durable subject.

An external identity without remote-status capability retains local session expiry and revocation. A registered status provider reporting an outage is not silently ignored. Admission providers must reject pending, rejected and suspended identities. A provisioning provider must atomically enforce uniqueness, maker/checker separation, expected revision, source revalidation, initial-profile activation and audit. Changed source data must be reviewed again. The generic administration page is available only when a provisioning provider is registered; `security.identity.submit` and `security.identity.approve` are separate local permissions.

## Optional second authentication

`Security:SecondFactor:Enabled` defaults to `false`. No kernel is required to provide a handler. Enabling it without a compiled `ISecondFactorHandler` registration fails startup. The default kernel supplies none. Configuration never selects a handler class.

The generic `/second-factor` page accepts a code or a manual confirmation/poll. `BeginAsync` receives a stable identity and a fresh attempt ID, without the primary password. The handler returns a challenge expiring within ten minutes. `VerifyAsync` must bind the identity, attempt and challenge; limit attempts; and atomically consume successful proofs across all instances. `CancelAsync` invalidates a pending challenge. The host owns its concrete challenge mechanism and durable/shared storage.

The browser receives an encrypted pending cookie that is never accepted as a session. A full session is issued only after success. Existing simple sessions require reconnection when second authentication is activated. Persist and share Data Protection keys for replicated hosts using standard ASP.NET Core services. Custom non-web login surfaces must enforce the same proof sequence before asserting second-factor verification to the session manager.

`ew dev` supports neither corporate providers nor second authentication. It continues using only identities in `ew.dev.json`.

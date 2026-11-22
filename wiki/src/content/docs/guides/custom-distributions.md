---
title: "Custom CLI distributions (L12)"
description: "Compiled CLI extensions, shared development services and instance branding."
---

This page describes the source implementation undergoing L12 qualification. It does not imply a new public NuGet release. The existing published examples remain pinned to their documented package versions.

Reference `EnterpriseWorkflow.Cli.Core` from your .NET tool entry project:

```csharp
using EnterpriseWorkflow.Cli;
var builder = EnterpriseWorkflowCli.CreateBuilder(args)
    .UseCommandName("few")
    .ConfigureModules(OrganizationServices.ConfigureModules)
    .ConfigureDevelopmentServices((context, services) =>
        services.AddOrganizationServices(context.Configuration))
    .AddTemplates("Organization.Workflow.Templates", "2.3.0", "few-module");
return await builder.Build().RunAsync();
```

Standard commands are included. `AddCommand` supports compiled commands and subcommands with declared options; `ReplaceCommand` is explicit. Internal development commands and authentication services remain reserved. Development continues to use test identities from `ew.dev.json`.

The host registers its shared contract assemblies and its complete runtime identity/version. A module declares one `WorkflowRuntimeRequirement`, such as `Organization.Workflow` and `[2.3.0,3.0.0)`. Compatibility uses NuGet version ranges, without per-service declarations. Module authors own testing for their advertised range. Historical artifacts remain readable unless the host requires a declaration.

Run `few setup` explicitly before `few new few-module`. Template versions are fixed by the distribution; project generation does not install missing templates automatically. Feed credentials use normal NuGet mechanisms.

Use the hosting builder’s `ConfigureBranding` to set instance defaults, logo, favicon, stylesheet and palette. Custom hosts lock administrative fields by default; `EditableFields` grants explicit permissions. Persisted values cannot override locked fields, and server writes enforce the same policy. The standard Kernel explicitly preserves its historical editable settings.

The repository’s `samples/OrganizationWorkflow` demonstrates contracts, SDK, HTTP services, CLI, templates, Kernel and an external demo API. The step-by-step qualification is maintained in `docs/validation/l12-recette-porteur.md` in the source repository.

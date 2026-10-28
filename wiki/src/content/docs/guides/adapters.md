---
title: "Providers and adapters"
description: "Extend public contracts from a trusted composition root."
sidebar:
  order: 9
---

Choose the public abstraction that corresponds to the behavior you need to replace. A business handler is usually the right extension point for a domain operation; replacing persistence or identity requires a much wider behavioral contract.

| Extension | Contracts to inspect |
| --- | --- |
| Business execution | `IServiceNodeHandler`, `IDecisionNodeHandler`, `IHumanTaskCompletionHandler` |
| Business services | Module `ConfigureServices(IServiceCollection)` |
| External effects | `IExternalEffectTransport` |
| Authentication/directory | `IAuthenticationProvider`, `IIdentityDirectory` |
| Authorization | `IAuthorizationProvider`, `IWorkflowAuthorizationService` |
| Binary media | `IWorkflowMediaStorageProvider`, scanner and inspector contracts |
| Workflow persistence | `IWorkflowStore` and maintenance/migration contracts |

Use [the package reference](../../reference/packages/) to locate the contract assembly. Follow existing adapters and their tests as behavioral examples, not only their method signatures. Lease fencing, revision checks, idempotency, authorization and atomic source updates must survive an implementation change.

The bundled Kernel selects its built-in database/authentication options. Arbitrary adapter plugins are composed in your own host or supported module service registration; there is no documented JSON switch that instantiates any CLR type by name.

Validate the extension in an isolated installation with failure, cancellation and restart cases. See [advanced adapter contracts](../../advanced-topics/adapters/) for the review checklist.

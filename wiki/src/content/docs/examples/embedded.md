---
title: "Embedded application checklist"
description: "Turn the console composition example into an application integration."
sidebar:
  order: 4
---

Run [the embedded quickstart](../../getting-started/embedded/) first. Its generated `Program.cs` is a complete compilable composition example that uses the same module and definition as the Kernel.

## Preserve the composition order

1. Load verified module artifacts with `WorkflowModuleCatalog.Load`.
2. Construct the workflow/security database objects and apply migrations explicitly.
3. Create stores and reconcile inventory with `WorkflowModuleKernel.ReconcileAsync`.
4. Register stores, identity directories, profiles and authorization services.
5. Register modules, runtime, presentation and any media service needed by the module.
6. Start through `IWorkflowPortalService`, using the authenticated actor, qualified process ID, revision and submission.
7. Host workers or execute the pump according to your application's lifecycle.

The sample assigns only known permissions requested on the command line. Keep the actual authorization provider when replacing the demonstration identity with your application principal; do not replace it with an unconditional success stub.

## Add application responsibilities

Provide task query/claim/complete interactions, durable worker lifetime, cancellation/shutdown behavior, a real external-effect transport if effects are declared, and coherent backup/restore. If using media, call `AddEnterpriseWorkflowMedia` with a durable private root and preserve the storage provider contract.

The console template pumps immediately ready work and exits. That does not wait for future timers, approve a human task or provide an HTTP inbox. A web integration can compose the Web package deliberately; a headless integration can stay on application/runtime contracts.

Verify restart with a waiting instance, rejected access and an unavailable required artifact before using real data. [Kernel source](https://github.com/mbo2olivier/enterprise-workflow/blob/v1.0.0-rc.1/src/EnterpriseWorkflow.Kernel/Program.cs) provides a larger composition example.

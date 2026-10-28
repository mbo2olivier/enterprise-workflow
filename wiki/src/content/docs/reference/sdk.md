---
title: "DSL and public contracts"
description: "Find the builder and handler APIs used by the tutorials."
sidebar:
  order: 4
---

The fluent SDK is declared in `EnterpriseWorkflow.Sdk` and consumed by the `WorkflowModule` authoring surface. The public package graph includes `EnterpriseWorkflow.Extensions.Abstractions`, which the generated module references.

| API | Purpose |
| --- | --- |
| `WorkflowModule.Info` | Module ID, release, contract revision and metadata |
| `ConfigurePermissions` | Module-local permission declarations |
| `WorkflowModule.ConfigureServices` | Business dependency registration |
| `WorkflowModuleBuilder` | Handler, form, media policy and process declarations |
| `IWorkflowProcess.Configure` | Configure a process builder |
| `WithState<T>().InitializedBy<T>()` | Typed initial business contract |
| `WithStateSchemaVersion` | Explicit persisted state version |
| `StartWithForm` / `Then` | Start input and sequential transitions |
| `Input` / `Approval` | Custom form task or integrated approval |
| `Service<THandler>` | Automated business step |
| `End` | Terminal engine path and business outcome |
| `RequiresPermission` / `ReadableByPermission` | Action/start and instance reading requirements |
| `CommentAt` / `RequireCommentAt` | Built-in approval comment mapping |

Key execution contracts are in namespace `EnterpriseWorkflow.Runtime`: `IServiceNodeHandler`, `IDecisionNodeHandler`, `IHumanTaskCompletionHandler`, `IWorkflowStateInitializer` and `IWorkflowStateValidator`. Read state/submissions with the typed context helpers. Use `NodeStateUpdate.Replace` for complete state replacement. `IWorkflowSerializer` is in `EnterpriseWorkflow.Core.Serialization`.

Use `IWorkflowPortalService` for authorized presentation-level start/task interactions. Host composition uses `WorkflowModuleCatalog`, `WorkflowModuleKernel.ReconcileAsync`, `AddEnterpriseWorkflowModules` and `AddEnterpriseWorkflowRuntime<TTransport>`.

## State annotations

`JsonPropertyName` fixes JSON names. `Label` adds display text. `HiddenField`, `VisibleFor` and `VisibleForAll` restrict projections. `RequiredFor` and `StateValidator` add contextual validation. `CommentState` identifies collections of `WorkflowComment` for the dedicated comments view. Standard validation attributes include Required, StringLength, MinLength, MaxLength and Range within supported types.

This is a task-oriented reference, not a claim to list every overload. Consult [the pinned SDK source](https://github.com/mbo2olivier/enterprise-workflow/tree/v1.0.0-rc.1/src/EnterpriseWorkflow.Sdk), the generated module and [LeaveRequest](../../examples/leave-request/) for complete compilable declarations.

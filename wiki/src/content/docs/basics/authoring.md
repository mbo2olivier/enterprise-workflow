---
title: "Author a process"
description: "Connect typed state, forms, handlers and transitions."
sidebar:
  order: 3
---

Start from the generated module. Authoring has four connected parts:

1. Declare the business state DTO, its JSON names and validation attributes.
2. Implement `IWorkflowStateInitializer` to map a start-form submission into that DTO.
3. Register handlers and forms in `WorkflowModule.Configure`.
4. Declare the process through `IWorkflowProcess.Configure`, including state schema version, permissions, nodes and transitions.

For example, the template declares `RequestState`, `RequestInitializer`, a `request-form`, an approval, a closing service and accepted/declined end nodes. The process uses `WithState<RequestState>().InitializedBy<RequestInitializer>().WithStateSchemaVersion(1)`.

Keep the typed builder returned by `WithState<T>()` when configuring typed expressions. Form fields and state fields are independent: a field named `reason` is not automatically copied into a property named `Reason`. The initializer owns this mapping explicitly.

Run `ew validate .` after changing the declaration and `ew inspect .` to review its resolved structure. Missing handler bindings, unknown permissions, incompatible declarations and invalid contracts must be corrected before packaging.

Continue with [typed state](../../core-concepts/state/) and [handlers](../../guides/handlers/). For a complete declaration with multiple actors, follow [LeaveRequest](../../examples/leave-request/).

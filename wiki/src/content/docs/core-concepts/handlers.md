---
title: "Nodes, handlers and transitions"
description: "Bind durable keys to code and keep action outcomes explicit."
sidebar:
  order: 4
---

The persisted graph contains stable, versioned handler references. It does not serialize a CLR instance, service object or lambda. At composition time the registry binds those references to concrete handlers owned by exact artifacts.

| Contract | Role |
| --- | --- |
| `IWorkflowStateInitializer` | Build initial typed business state |
| `IServiceNodeHandler` | Execute automated business work |
| `IDecisionNodeHandler` | Select an exclusive decision outcome |
| `IHumanTaskCompletionHandler` | Validate/accept human completion and optionally replace state |

A missing, duplicate or incompatible binding prevents valid composition. Each execution attempt resolves its handler in a short DI scope. The caller's request scope is not kept alive while a process waits.

An input task handler returns `HumanTaskCompletionResult.Success()` to accept without changing state. The authorized action already identifies the transition; the handler does not need to repeat an outcome. Decision-node outcomes remain explicit and distinct from this human-action behavior.

Integrated approvals use the built-in handler and comment mapping. Custom `HandledBy` completion belongs to form/input tasks rather than replacing approval-node behavior. Services continue to use their business handlers.

See [writing handlers](../../guides/handlers/) for DI, state updates and cancellation.

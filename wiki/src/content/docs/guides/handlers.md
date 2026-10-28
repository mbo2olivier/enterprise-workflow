---
title: "Handlers and business services"
description: "Register dependencies and implement bounded business work."
sidebar:
  order: 1
---

Start from the generated module. Register each handler with the module builder, for example `.AddService<CloseHandler>("close")`, and use `.Service<CloseHandler>("close")` in the process graph. The module resolves the stable reference associated with that type.

A service handler implements `IServiceNodeHandler`. The generated `CloseHandler` reads the full typed state, marks it closed and returns a replacement. To inject your own business dependency, override the module's service configuration:

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IMyBusinessService, MyBusinessService>();
}
```

This is a fragment inside your `WorkflowModule`; add `using Microsoft.Extensions.DependencyInjection` and supply the interface/implementation in your module. Services are resolved within each execution scope.

Use `context.ReadState<T>()` for detached state. An input-task handler can additionally read `context.ReadSubmission<TInput>()`. Return the appropriate result contract and use `NodeStateUpdate.Replace` only with a complete state DTO.

Honor the cancellation token and configure network timeouts below the attempt budget. External calls need an idempotency contract because a process crash can happen after remote success and before the local engine commit. Use the [service integration tutorial](../../examples/service-integration/) when adding remote operations.

Initializers and state validators are not the place for external business side effects. They map or validate data deterministically before a durable commit.

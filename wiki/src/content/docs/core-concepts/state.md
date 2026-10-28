---
title: "Typed business state"
description: "Map, validate and replace durable state explicitly."
sidebar:
  order: 3
---

Every authored process declares a root DTO, an initializer and an explicit schema version. Use `WithState<TState>().InitializedBy<TInitializer>().WithStateSchemaVersion(version)` and keep the returned typed builder.

The initializer maps validated submission values into durable state. There is no automatic field-name mapping from form to state. It must be deterministic for the submitted input and avoid external side effects. Inject `IWorkflowSerializer` to serialize the initial DTO.

## JSON and validation

Persisted names follow the DTO properties or `[JsonPropertyName]`; no implicit camel-case convention is applied. Unknown JSON properties and incompatible types are rejected. Objects, lists, strings, booleans, numbers, `DateOnly`, `DateTimeOffset`, GUIDs and nullable supported types can form a non-cyclic contract.

Annotations such as `Required`, `StringLength` and `Range` become server-side constraints. `RequiredFor("approval")` and `.ValidateState("approval")` add contextual rules. A `[StateValidator(typeof(MyValidator), "approval")]` supplies a pure cross-field validator implementing `IWorkflowStateValidator`.

```csharp
var state = context.ReadState<RequestState>();
state.Closed = true;
return ServiceNodeResult.Success(NodeStateUpdate.Replace(state));
```

The read returns a detached object. Editing it alone changes nothing. `Replace<T>` serializes a **complete replacement**, not a patch; a partial DTO would lose unrelated fields. Runtime validation and serialization budgets apply before the commit.

## Visibility

`Label` controls display labels; `HiddenField` excludes a field from the detail projection. `VisibleFor` and `VisibleForAll` restrict projections by declared module permissions. Newly declared fields are visible to authorized instance readers by default. Review this before publishing sensitive fields.

Form audiences, DTO visibility and media policies are independent. See [forms](../../guides/forms/) and [media](../../guides/media/).

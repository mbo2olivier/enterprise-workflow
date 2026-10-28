---
title: "Forms, components and selectors"
description: "Compose server-validated input and informative Razor views."
sidebar:
  order: 3
---

Declare fields with the module's form builder. For example, the template uses `.Text("reason", "Motif").Required().Length(3, 200)`. LeaveRequest adds dates, choices, long text and a file field.

A form's completion handler is declared independently of its registration. Register the handler in the module, use `.HandledBy<T>()` on the form/input task and map submitted values explicitly. Form values are not automatically merged into business state or prefilled by matching property names.

`ReadableBy` controls a field's audience. DTO annotations control state projection. Review both for private data: a field hidden on the start form can still be exposed if copied into unrestricted state.

## Informative custom component

`.WithComponent<LeaveRequestSummary>("leave-request.summary")` associates a Razor view with the form. A component can receive `CanonicalJson? Values` and `CanonicalJson? State` as `[Parameter]` properties. `Values` contains server form values; `State` contains authorized projected state. Unsaved browser typing is not server data for this SSR view.

The framework retains generated fields and submission controls. The component is an informative view rather than an unrestricted replacement for the commit protocol. Its assembly/resources remain owned by the module artifact.

## Remote candidate selection

For assignment to a later task, declare `.SelectAssigneeFor("validation-manager")` on the requester confirmation action and `.AssignedFromPreviousTask()` on the target. Candidates are searched and validated against the target's eligibility. Test an unauthorized candidate and a permission revoked after selection; browser filtering alone is insufficient.

[LeaveRequest](../../examples/leave-request/) demonstrates the view and the requester-to-manager selector.

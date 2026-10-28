---
title: "Definitions, instances and graphs"
description: "Distinguish the durable process contract from each execution."
sidebar:
  order: 1
---

A **definition** is a versioned graph describing nodes, transitions and their configuration. An **instance** is one durable execution of that definition, with its own business state, activations, human tasks and history.

A definition is immutable once published. An existing instance does not silently adopt changes when an author publishes another module release. The instance retains the definition and artifact identities required for execution and presentation.

The delivered model supports sequential flow and exclusive decisions. A node can represent human work, a service, a decision, durable waiting or termination within the supported DSL. Loops and parallel branches are outside this release. The compiler validates the graph before it becomes executable.

## Business outcomes

A terminal engine status and a business result answer different questions. A process can complete with a successful, declined or cancelled business outcome. The sample explicitly declares these with `WorkflowCompletionOutcome.Succeeded`, `.Declined` and `.Cancelled` on its end nodes.

Use the ten-character public reference for human lookup and the GUID for technical identity. A public reference is neither a secret nor proof of authorization. All reads still pass access checks.

See [the builder reference](../../reference/sdk/) and [LeaveRequest](../../examples/leave-request/) for the relationship between actions, transitions and terminal outcomes.

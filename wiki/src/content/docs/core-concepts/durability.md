---
title: "Durable execution"
description: "Understand commits, retries, waiting and external effects."
sidebar:
  order: 5
---

Execution persists state and scheduling information so that waiting does not depend on a live HTTP request. Workers acquire bounded leases to execute ready activations. A restart can resume work using the same definition and exact handler artifacts.

A successful commit checks ownership, lease generation and state revision. An obsolete worker must not commit after losing its lease. This fencing protects engine data; it cannot reverse an HTTP request already accepted by an external system.

## Retrying is not exactly-once delivery

A handler may execute again after a timeout or crash. Make externally visible operations idempotent with a stable operation key and a receiving system that enforces it. Do not generate a different business operation on every retry.

The outbox separates durable intent from delivery. Its own leases, retry policy and errors govern external transport work. Delivery can be retried; a recipient must tolerate duplicates. The engine does not promise a transaction across your database and an unrelated remote service.

Durable waiting records the appropriate schedule rather than sleeping a handler for days. Keep handler attempts bounded and honor cancellation.

Embedded hosts configure `WorkflowRuntimeOptions`. The Kernel binds serialization settings under `Workflow:Serialization`; it does not bind every worker option from arbitrary JSON keys. See [runtime reference](../../reference/runtime/) and [recovery](../../advanced-topics/recovery/).

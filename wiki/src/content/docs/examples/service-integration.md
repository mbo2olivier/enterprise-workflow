---
title: "Integrate a business service"
description: "Add external work without assuming exactly-once execution."
sidebar:
  order: 3
---

Use the generated module's closing service as the integration point. It already implements `IServiceNodeHandler` and participates in durable execution. Replace its trivial business operation with your installation's service dependency, registered through `ConfigureServices`.

## Define the receiving contract first

Suppose closing an approved request must register leave in another application. Give that application a stable operation key derived from the workflow instance and business operation, and require it to return the original result when the same key is repeated. Do not use a new random key on each execution attempt.

The handler reads the complete state, sends the bounded request with the cancellation token, then returns its state update only after interpreting the response. The external write and engine commit are not one distributed transaction: a crash between them can cause the handler to call the receiver again.

If the operation is modeled as an external effect, implement the `IExternalEffectTransport` contract in the host. Its result distinguishes delivered work from retryable or permanent failures. The embedded template intentionally supplies an unconfigured transport that permanently fails external effects rather than pretending delivery succeeded.

## Verify failure boundaries

- A timeout before remote success should follow the configured retry policy.
- A remote success followed by local interruption should be safe to repeat with the same key.
- A permanent business rejection should remain distinguishable from a temporary connection failure.
- A cancelled or obsolete attempt must not commit a stale state update.

Your service integration is ready only when the receiver's idempotency behavior is observed. Engine fencing alone cannot deduplicate the remote service. See [recovery and concurrency](../../advanced-topics/recovery/).

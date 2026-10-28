---
title: "Adapter implementation contracts"
description: "Review durability and authorization before replacing infrastructure."
sidebar:
  order: 4
---

An adapter implementation must preserve behavioral contracts, not only satisfy an interface. Start with the public abstractions and bundled implementations in the [release source](https://github.com/mbo2olivier/enterprise-workflow/tree/v1.0.0-rc.1/src).

For a persistence adapter, verify schema migration lifecycle, atomic state/task/outbox/audit/TAT changes, optimistic revisions, lease expiry and renewal, fencing, idempotency and bounded query behavior. A store that returns success before all required writes are durable changes the runtime's guarantees.

For an identity/directory adapter, preserve provider-qualified stable subjects, bounded search, cancellation, explicit unavailable/refused outcomes and revocation behavior. Never translate a transport error into successful authentication or universal eligibility.

For a media provider, keep objects private and immutable as required, enforce content identity, cooperate with quarantine and logical attachment/deletion, and define coherent backup/restore. Storage object existence alone does not authorize downloading it.

For a transport, distinguish success, retryable failure and permanent failure and document recipient idempotency. Honor the delivery cancellation budget.

Run contract and integration scenarios against the real adapter, including concurrent attempts, service failure and restoration. A new adapter has its own qualification matrix; the SQLite, Oracle or OpenLDAP results do not automatically extend to it.

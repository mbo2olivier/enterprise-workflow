---
title: "Concurrency, fencing and recovery"
description: "Know which guarantees end at the engine commit boundary."
sidebar:
  order: 2
---

Execution leases identify the worker attempt allowed to mutate an activation. A renewed lease has a bounded lifetime; a later owner can supersede an expired one. Generation/fencing checks prevent the old owner from committing engine work afterward.

Optimistic state revisions protect concurrent updates. Human actions are checked against current permissions and task state before commit. Media attachment and mutation operations recheck their scope and revision with the source transaction. A prevalidated browser form is not sufficient authority to commit later.

## Failure windows

| Failure window | Consequence to design for |
| --- | --- |
| Before commit | The attempt may run again with the previous committed state |
| After remote call, before engine commit | Remote operation may be repeated; receiver needs idempotency |
| After storing bytes, before media commit | Private invisible staging/orphan objects may need maintenance |
| After backup point and external delivery | Restore may replay delivery already observed outside the installation |

A successful lease protocol does not create exactly-once effects in another system. Keep remote operation keys stable, use bounded timeouts and honor cancellation. Separate a permanent business refusal from a transient infrastructure failure.

Test a waiting instance across process restart, an expired/stale attempt, concurrent human completion and failed external delivery. Use actual target stores for adapter qualification rather than only mocks.

Defaults and retry limits are in [runtime reference](../../reference/runtime/); recovery of a full installation is covered by [backup/restore](../../operations/backup/).

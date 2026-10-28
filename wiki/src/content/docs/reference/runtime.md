---
title: "Runtime options and defaults"
description: "Worker, lease, retry and serialization defaults for embedded composition."
sidebar:
  order: 5
---

Configure `WorkflowRuntimeOptions` through `AddEnterpriseWorkflowRuntime<TTransport>(options => ...)` in your composition root. These defaults apply to the released runtime:

| Property | Default |
| --- | --- |
| `WorkerConcurrency` | 1 |
| `LeaseDuration` / `LeaseRenewalInterval` | 30 s / 10 s |
| `IdlePollingInterval` | 250 ms |
| `MaximumAttempts` | 5 |
| `RetryBaseDelay` / `RetryMaximumDelay` | 1 s / 30 s |
| `AttemptTimeout` | 5 min |
| `OutboxConcurrency` | 1 |
| `OutboxLeaseDuration` | 30 s |
| `OutboxIdlePollingInterval` | 250 ms |
| `OutboxMaximumAttempts` | 10 |
| `OutboxRetryBaseDelay` / `OutboxRetryMaximumDelay` | 1 s / 1 min |
| `TimerIdlePollingInterval` | 250 ms |
| `HumanTaskInboxMaximumScan` / `HumanTaskInboxPageSize` | 500 / 50 |
| `HumanTaskMaximumSubmissionBytes` | 256 KiB |
| `NotifyHumanTaskPools` | false; enabled by configured notification composition |

Durations must be positive whole milliseconds; lease renewal must be shorter than the lease. Retry base delay cannot exceed its maximum. Inbox page size cannot exceed its scan limit, capped at 10,000. Human submission configuration is capped at 4 MiB.

`Serialization.MaximumStateBytes` defaults to 1 MiB and `MaximumDepth` to 64. You can lower these engine budgets, not exceed their ceilings. The DTO schema compiler additionally bounds non-cyclic structure depth to 12 and object properties to 128. These are different limits: a JSON parser budget does not extend the supported DTO contract.

Budgets apply at initialization, human/service commits and migrations. Reducing them may reject future writes for an older large state; it does not migrate that state automatically.

The packaged Kernel binds `Workflow:Serialization`, and development binds `serialization`. Neither exposes every worker option through an undocumented configuration key. Size concurrency/timeouts for the actual service, database and receiver behavior; no performance guarantee is implied by a default.

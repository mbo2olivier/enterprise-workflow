---
title: "Audit and reporting reference"
description: "Export bounds and a calendar-time reporting query."
sidebar:
  order: 7
---

Audit requires `security.audit.read`; CSV export requires `security.audit.export`. TAT uses `workflow.tat.read` and `workflow.tat.export`. These capabilities do not grant instance business access or task completion.

Audit export configuration through `AddEnterpriseWorkflowPresentation`'s `configureAudit` callback supports row limits up to 10,000, default byte budget 16 MiB and default timeout 30 seconds. The exporter refuses over-budget output rather than silently truncating it. CSV is UTF-8 with spreadsheet-formula protection.

## TAT source data

`EW_TAT_INSTANCES`, `EW_TAT_STEPS`, `EW_TAT_ASSIGNMENTS` and `EW_TAT_EVENTS` are additive transactional projections. Timestamps are UTC milliseconds. `Coverage = 1` means complete observation; partial historical records need separate treatment.

A read-only SQLite/Oracle query for closed-step calendar duration:

```sql
SELECT i."ProcessId", i."ProcessVersion", s."NodeId", COUNT(*) AS observations,
       AVG(s."EndedAtMs" - s."StartedAtMs") AS mean_ms
FROM "EW_TAT_STEPS" s
JOIN "EW_TAT_INSTANCES" i ON i."Id" = s."InstanceId"
WHERE i."Coverage" = 1 AND s."Coverage" = 1
  AND s."EndedAtMs" IS NOT NULL
GROUP BY i."ProcessId", i."ProcessVersion", s."NodeId";
```

This query has no date filter: add a deliberate reporting period before querying a large installation. Web authorization does not protect direct SQL; grant a controlled read-only reporting account.

For analyst measures, sum one actor's intervals per completed task before computing the mean across tasks. Group by provider and subject, not just display name. Open intervals and pool time are separate from completed assigned intervals; none measures active human effort.

The UI refuses more than 10,000 observations. `IWorkflowMaintenanceStore.RebuildTatAsync` rebuilds one instance from retained TAT events transactionally, with a 50,000-event ceiling. See [audit and TAT guide](../../guides/audit-tat/).

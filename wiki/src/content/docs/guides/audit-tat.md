---
title: "Audit, exports and TAT reports"
description: "Inspect operational history and interpret calendar turnaround time."
sidebar:
  order: 7
---

The administration audit combines security events and the transactional business journal. `security.audit.read` permits reading; `security.audit.export` permits export. Both table and CSV use server filters. An export is prepared within row, byte and timeout limits and is audited before delivery. Spreadsheet formula-like values are neutralized in CSV output.

Capabilities `workflow.instance.search.initiator`, `workflow.tat.read` and `workflow.tat.export` are independent of instance business read rights. Existing profiles are not silently granted new capabilities after an upgrade; update them explicitly.

## Turnaround time

`/admin/tat` groups observations by process/revision/step, optionally by analyst. Mean/min/max cover **closed intervals** in the selected period. Open counts and ages are separate. An analyst's assignment duration is elapsed calendar time under assignment, not measured human working time.

Source transactions populate `EW_TAT_INSTANCES`, `EW_TAT_STEPS`, `EW_TAT_ASSIGNMENTS` and `EW_TAT_EVENTS`. `Coverage = 1` denotes complete observations; older instances can have partial coverage. The UI refuses over 10,000 observations rather than returning an apparently complete aggregate from a truncated set.

[The reporting reference](../../reference/reporting/) includes SQL and export limits. A direct SQL connection bypasses web permissions; use a separately controlled reporting account.

For LeaveRequest, compare time spent waiting in the HR pool with time assigned to Bob. Those intervals answer different operational questions. Do not label either interval as active effort or a business-hours SLA without additional instrumentation.

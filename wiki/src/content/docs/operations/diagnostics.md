---
title: "Observe and diagnose"
description: "Investigate failures without rewriting durable state by hand."
sidebar:
  order: 5
---

Begin with host startup diagnostics, the affected module/release and the instance's technical ID. Authorized instance detail shows recent progression; administrative audit and TAT provide different views of the same installation's activity.

| Symptom | Check | Next action |
| --- | --- | --- |
| Host refuses startup | Pending schema, connection, catalogue and identity settings | Fix the explicit error and rerun migration/validation as appropriate |
| Task not visible | Provider/subject, read permission, eligibility, assignment and task status | Correct the intended profile or mapping through administration |
| Rebuild fails locally | Compiler output and watched inputs | Fix source; previous host remains active after a compile failure |
| Retained instance cannot resume | Exact required artifact and API compatibility | Restore the owner artifact or compatible runtime; do not substitute a version |
| LDAP login fails | TLS trust/name, bind identity, filters, stable ID and availability | Repair directory configuration without disabling verification |
| Notification delayed | Outbox state, retry budget, relay and recipient mapping | Restore transport and inspect delivery outcome |
| Search cursor rejected | Actor/filter changes or host restart | Start a new search |
| Media unavailable | Attachment policy, inspection/quarantine, private volume | Fix the cause while preserving attachment metadata |

Carry correlation identifiers into business-service logs where possible. Avoid logging passwords, full submissions or confidential state. Instance detail's last 100 operations is not a replacement for a retained audit/export policy.

A failed attempt can be retried under policy; a terminal failure does not imply a universal manual “resume anything” operation. The release provides no generic arbitrary state editor. Investigate the original cause and use only a defined, tested maintenance procedure.

See [runtime defaults](../../reference/runtime/) and [recovery semantics](../../advanced-topics/recovery/).

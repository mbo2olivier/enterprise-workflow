---
title: "Authorized instance search"
description: "Find running and completed instances without disclosing forbidden data."
sidebar:
  order: 5
---

Use the search entry in the web UI or `/search`. Filters include process, status and dates; status selection supports multiple values. Search by public request reference or technical GUID. Civil-date filters use the installation's timezone.

Results and instance detail are filtered on the server for the current actor. `ReadableByPermission("read")` in a process declares a business reading requirement; granting task participation or administrative search capability does not automatically grant that read permission.

The detail page remains available for authorized completed instances. It shows filtered state, progression and recent engine operations. Recent history is bounded to the last 100 operations; it is not an unbounded audit export.

Search cursors are encrypted and bound to actor and filters. Restarting the current local implementation invalidates cursors; restart the search rather than reusing the old cursor. A changed permission can invalidate access even when a previous page displayed the instance. No global count of unauthorized instances is exposed.

To test with LeaveRequest, create instances as Alice, read them as authorized users and revoke a reader's permission through administration. Refresh both search and a previously opened detail URL to confirm the server denies access.

For administrative capabilities, exports and reporting, see [audit and TAT](../audit-tat/).

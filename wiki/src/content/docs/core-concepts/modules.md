---
title: "Modules and versions"
description: "Understand module release, process revision and state schema version."
sidebar:
  order: 2
---

A module packages process declarations, handlers, permissions, forms and resources. Its manifest identifies the module, release and hashed contents. These identifiers are part of durable ownership, not just assembly deployment metadata.

Keep three numbers distinct:

| Version | Meaning | LeaveRequest example |
| --- | --- | --- |
| Framework NuGet version | Engine/SDK/tool release | `1.0.0-rc.1` |
| Module release and contract revision | Immutable business artifact and declared contracts | `1.8.0`, revision `9` |
| State schema version | Shape and validation of persisted business data | `4` |

The current sample's process is `sample.leave-request.leave-request`, revision `9`. A production change that redeclares contracts requires an explicit compatible release/revision strategy. A local `ew dev` generation is a separate, reserved development identity.

When an instance still references an old artifact, retain that artifact's code and resources. Copying a newer DLL over an old release breaks exact ownership even if the CLR type name is unchanged. Explicit references to another artifact's process, form or handler retain that owner rather than transferring its ownership.

See [evolving a running workflow](../../examples/evolution/) and [artifact compatibility](../../advanced-topics/versioning/).

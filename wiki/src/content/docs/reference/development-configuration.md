---
title: "ew.dev.json reference"
description: "Configure named development environments with schema version 2."
sidebar:
  order: 2
---

The implicit configuration is `ew.dev.json` at the module project root. `--config` selects another file. Paths are relative to the configuration file's directory, or to the project root if no file exists.

```json
{
  "schemaVersion": 2,
  "defaultEnvironment": "default",
  "build": { "configuration": "Debug", "watch": true },
  "server": { "url": "http://localhost:5080" },
  "serialization": { "maximumStateBytes": 1048576, "maximumDepth": 64 },
  "moduleDirectories": [],
  "seed": {
    "users": [
      { "id": "alice", "displayName": "Alice", "permissions": ["acme.leave.request", "acme.leave.read"] },
      { "id": "bob", "displayName": "Bob", "permissions": ["acme.leave.review", "acme.leave.read"] }
    ]
  },
  "environments": {
    "review": { "server": { "url": "http://localhost:5100" }, "build": { "watch": false } }
  }
}
```

This seed applies to the generated `acme.leave` module. Unknown permissions are refused. Use the sample's own seed for LeaveRequest.

| Setting | Default / constraint |
| --- | --- |
| `schemaVersion` | `2`; legacy `1` accepted |
| `defaultEnvironment` | `default` |
| `build.configuration` | Debug or Release |
| `build.watch` | true |
| `server.url` | HTTP loopback root, no path/query/credentials |
| `storage.provider` | `sqlite` only |
| `storage.path` | Selected environment's `workflow.db` |
| `moduleDirectories` | Empty; all supplied artifacts must validate |
| `seed.users` | Up to 100 users, unique simple IDs, explicit known permissions |
| `smtp` | Disabled without a host; see [SMTP guide](../../guides/notifications/) |
| `serialization` | At most 1 MiB state and JSON depth 64 |

Environment overrides support build, server, storage, seed, SMTP, serialization, module directories and legacy watch settings. Precedence is CLI → selected environment → common → defaults. Names are case-insensitive, at most 64 ASCII letters/digits/hyphens/underscores, starting with a letter/digit; reserved device names are refused.

The local subject is `ew-dev-<id>` under provider `local`. The seed initializes once; later admin revocations survive restarts. Reset only archives data owned by the selected environment; an explicit external database path is not resettable through this operation.

Properties not recognized by the configuration contract are refused. YAML, Oracle and LDAP are not development-host configuration options; use the Kernel or an embedded composition for those providers.

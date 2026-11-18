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

## Local environment files (L11)

The L11 source implementation loads an optional `.env` from the source project root, or from the artifact directory in artifact mode. This addition is not part of the previously published CLI release.

```sh
ew dev
ew dev --env-file .env.local
ew dev --no-env-file
```

An explicit path is relative to the current working directory and must exist. `--env-file` and `--no-env-file` cannot be combined. Named development environments do not select `.env.<name>` implicitly. Restart `ew dev` after editing the file.

```dotenv
Development__Server__Url=http://localhost:5080
Development__Build__Configuration=Debug
Notifications__Smtp__Password=replace-locally
Localization__DefaultCulture=en
```

Precedence is CLI options → process environment → `.env` → selected/common `ew.dev.json` → defaults. `Development__Server`, `Development__Storage`, `Development__Build`, `Development__Watch` and `Development__Serialization` target development configuration. `Development__DefaultEnvironment` supplies the default named environment. SMTP, localization and custom module configuration values are passed only to the host. For example, `ExternalServices__Payments__ApiKey` is available as `ExternalServices:Payments:ApiKey` through host configuration. Identity and seed configuration stays in `ew.dev.json`; security keys never activate external authentication or a second factor in development.

The parser supports `KEY=value`, blank lines, comments and single/double quoted values. It performs no shell execution or variable interpolation. Duplicate or malformed entries are rejected without displaying their values. Environment-file secrets are not placed in runtime JSON snapshots, command arguments, packaged artifacts or build-process environments. Do not commit local `.env` files; use `.env.example` for non-sensitive examples. Production kernels keep standard .NET configuration and do not automatically read `.env`.

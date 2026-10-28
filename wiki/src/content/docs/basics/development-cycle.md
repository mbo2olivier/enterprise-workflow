---
title: "Local development cycle"
description: "Work with named environments, rebuilds and immutable local generations."
sidebar:
  order: 2
---

From a module directory:

```bash
ew dev .
ew dev . --environment hr-review --url http://localhost:5100
ew dev . --environment hr-review --no-watch
```

Each environment owns `.ew/environments/<name>` beneath the project. Names are normalized without case distinctions. Separate environments have separate databases and local registries. Configuration precedence is CLI options, selected environment overrides, shared settings, then defaults.

## Rebuild and resume

Source changes produce a local generation identified by the built DLLs, dependencies and resources. Unchanged builds reuse a generation. Existing instances retain the exact artifacts they require; new instances use the current generation. A failed compilation leaves the previous host running. Changes outside watched source paths, or changes to `ew.dev.json`, require an explicit restart.

You do not need to increment the production module release for every local edit. Development releases use reserved `-ewdev.gN` identities and internal revisions; production verification and the Kernel reject them.

## Reset deliberately

```bash
ew dev . --environment hr-review --reset
```

Reset archives the selected owned environment before recreating it. It does not erase external databases or import old `.ew/dev` directories. Seed users initialize once: restarting does not restore permissions that an administrator revoked. Use another environment or reset when intentionally testing a different seed.

Only one session may own an environment. Stop with Ctrl+C before launching another owner. If startup fails after a rebuild, inspect the reported error rather than deleting retained data: those artifacts may still be needed by existing instances.

The [configuration reference](../../reference/development-configuration/) lists supported settings.

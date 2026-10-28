---
title: "Versioning and state migrations"
description: "Treat immutable ownership and schema transformations as separate contracts."
sidebar:
  order: 1
---

A module release identifies an immutable artifact. Its contract revision versions redeclared definitions/forms/handlers. Its state schema version identifies persisted DTO shape and validation. None is a substitute for the others.

Existing instances retain exact artifact references. A reference must resolve to the artifact's original ID, release and hash. If a new module reuses a published process, form or handler, preserve that ownership explicitly; do not reinterpret the old reference through a newer implementation.

## Explicit typed migrations

The migration registry supports `Add<TMigrator, TTargetState>(source, target, validationGroups)`. The runtime validates the target contract and validators before committing a typed state migration. A typed migration without the target contract is refused.

Migration keys remain artifact/source/target. The release does not add automatic chains or per-process migration keys. If multiple DTOs share a module, choose schema numbers and migration paths with that scope in mind. Referenced artifacts must already be available; migration does not silently add a new owner.

Test transformation with valid and invalid states, required contextual fields, missing validators and an interrupted attempt. Verify that failure leaves the prior committed state usable. Do not claim migration success just because a new definition builds.

See [the migration registry source](https://github.com/mbo2olivier/enterprise-workflow/blob/v1.0.0-rc.1/src/EnterpriseWorkflow.Extensions/WorkflowStateMigrationRegistry.cs) and [the evolution tutorial](../../examples/evolution/). The new first-public contracts do not promise compatibility with every pre-release prototype snapshot.

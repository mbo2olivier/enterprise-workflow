---
title: "Evolve a running workflow"
description: "Change code while preserving exact behavior for existing instances."
sidebar:
  order: 5
---

Start with the generated module and leave a request waiting for Bob. During local source development, change the closing label or handler and let `ew dev` rebuild. The runtime retains the generation required by the waiting request, while new requests use the selected generation.

This development mechanism is not the production release procedure.

## Production exercise

1. Package the original module into a new output directory and start an instance with it.
2. Retain that verified artifact unchanged.
3. Change the module release and the contract revision for redeclared process/form/handler contracts before packaging the new release.
4. Supply both required artifact directories to validation and the target catalogue.
5. Start a new instance on the new revision and complete the old waiting instance.
6. Inspect the two instances: each must retain its own definition and exact handlers/resources.

When a new release reuses an existing contract, reference its exact owner explicitly rather than redeclaring it under the old identity. Never overwrite old DLLs or remove artifacts simply because they are no longer the latest release.

## State shape changes

A state schema version is separate from the module release and process revision. A DTO shape change may require an explicit migration with a target state contract and validators. Raising a version number alone does not transform stored JSON.

Migration registration uses `Add<TMigrator, TTargetState>(source, target, validationGroups)` on the migration registry. Migration keys are artifact/source/target; automatic chaining and per-process migration keys are not introduced by this release. Plan schema numbers accordingly.

[Versioning and migrations](../../advanced-topics/versioning/) explains the constraints. Verify the exact migration against a disposable restored dataset before production.

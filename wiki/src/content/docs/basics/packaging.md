---
title: "Validate and package a module"
description: "Produce a verified immutable production artifact."
sidebar:
  order: 5
---

Run from the module's directory:

```bash
ew validate .
ew inspect .
ew inspect . --format json --output ./artifacts/resolved-module.json
ew pack . --configuration Release --output ./artifacts
ew verify ./artifacts/acme.leave/1.0.0
```

`pack` builds and validates a production module, then emits `<output>/<module-id>/<module-release>`. It includes the manifest, managed assemblies, declared dependencies and resources. It refuses to overwrite an existing artifact directory. `verify` reads and checks an artifact without changing it.

The module project must expose one public concrete fluent `WorkflowModule` in its target assembly. The initial packaging format rejects native/RID-specific dependencies and satellite assemblies rather than silently producing an incomplete package.

For declarations that reuse contracts owned by another artifact, provide its exact directory:

```bash
ew validate . --module-directory ../retained-module/1.0.0
ew pack . --output ./new-artifacts --module-directory ../retained-module/1.0.0
```

`--module-directory` is repeatable. Referenced owners remain separate artifacts; deploying the new module does not remove the obligation to retain those owners.

To test a packaged artifact locally, provide an external configuration file:

```bash
ew dev --artifact ./artifacts/acme.leave/1.0.0 --environment qualification --config ./ew.dev.json
```

Artifact mode performs no source build or watch. Do not combine it with `--configuration` or `--no-watch`. See [module deployment](../../operations/modules/) before installing it in the Kernel.

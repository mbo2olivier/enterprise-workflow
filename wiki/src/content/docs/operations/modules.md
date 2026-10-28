---
title: "Deploy and update modules"
description: "Install verified artifacts while retaining versions used by existing instances."
sidebar:
  order: 3
---

Package in the module project with `ew pack` and verify with `ew verify`. Transfer the entire artifact directory: manifest, assemblies, dependencies and resources. Do not transfer a local `.ew` generation.

Configure `Modules:Directories` with each exact required artifact directory. The Kernel loads the catalogue and reconciles inventory before serving the installation. It does not watch a module folder and hot-swap code into running scopes.

## Deployment procedure

1. Inspect and verify the artifact and any referenced owners.
2. Identify old versions still required by instances, forms and handlers.
3. Back up the installation coherently and stop the host under the operational procedure.
4. Install the new release under an immutable version directory; retain required old artifacts.
5. Update catalogue configuration and restart with the intended runtime and schema.
6. Check startup diagnostics, permissions, catalogue and an authorized business path.
7. Complete a waiting instance from the old release and start one from the new release.

If a required owner is absent, restore that exact artifact. Substituting the latest DLL is not recovery. An artifact hash verifies content identity; it is not a cryptographic publisher signature or a sandbox for untrusted code.

The module defines permissions; the installation assigns them. A module update does not grant new permissions to existing profiles silently. Review additions before enabling new process paths.

See [evolution](../../examples/evolution/) and [module loading limits](../../advanced-topics/modules/).

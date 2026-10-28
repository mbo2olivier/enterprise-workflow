---
title: "Embed in an application"
description: "Run the generated embedded console host against a packaged module."
sidebar:
  order: 4
---

First complete [your first workflow](../first-workflow/) through packaging. From the directory containing `Acme.Leave`, generate a sibling host:

```bash
dotnet new ew-embedded --name Acme.Portal
cd Acme.Portal
dotnet build
dotnet run -- ../Acme.Leave/artifacts/acme.leave/1.0.0 acme.leave.request 1 '{"reason":"Embedded demonstration"}' acme.leave.request acme.leave.read
```

The JSON quoting shown is for Bash/zsh. In PowerShell, pass the same JSON as one string argument using your shell's native argument quoting.

Expected output includes `Instance:` and a SQLite database path. The process ID is module-qualified; `1` is its declared process revision. Use a freshly created artifact from the installed release rather than an older development generation.

## What the template composes

The generated `Program.cs` loads the artifact catalogue, applies workflow and security migrations, reconciles module inventory, registers the SQLite stores and authorization provider, adds modules/runtime/presentation, assigns only the permissions passed on the command line and starts an instance through `IWorkflowPortalService`.

It pumps ready execution work until none is immediately executable. A human task remains durable in `embedded-workflow.db`; the console application does not provide an inbox or an interactive approval UI. It deliberately returns a permanent failure for external effects because it has no configured transport.

Use this as a composition example. Your application supplies a trusted authenticated identity, authorization profiles, task interactions, durable worker hosting and a configured external-effect transport. Do not turn the template's demonstration identity into a public authentication mechanism.

See [the embedded example](../../examples/embedded/) for the integration checklist.

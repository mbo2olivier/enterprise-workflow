---
title: "Your first workflow"
description: "Generate a module and complete an approval with two demo users."
sidebar:
  order: 3
---

After [installing the CLI and templates](../install/), run:

```bash
dotnet new ew-module --name Acme.Leave --module-id acme.leave
cd Acme.Leave
ew validate .
ew dev .
```

Open the loopback URL printed by `ew dev`. It builds the module, initializes SQLite, registers the declared permissions and seeds the demo identities from `ew.dev.json`.

## Complete the process

1. Select **Alice** and start a new request. Enter a reason of 3–200 characters and submit it.
2. Switch to **Bob** using the development identity selector. Find the approval in his inbox and claim it.
3. Approve it, optionally adding a comment. The closing service marks the state as closed and the instance reaches its accepted terminal outcome.
4. Open the completed instance as an authorized reader. Inspect the state, comments and recorded progression.

Repeat with a new request and decline it. Decline requires a comment and reaches the declined outcome. Alice cannot approve her own request by default. The local administration identity manages permissions but has no implicit business access.

The template's visible business labels currently use French: **Motif**, **Approuver**, **Refuser**. You can change those labels in the module; permission IDs and persisted JSON names are separate contracts.

Stop with Ctrl+C. Run `ew dev .` again: the database and waiting tasks remain. To start a separate local dataset, use `ew dev . --environment tutorial`.

## Package the declared release

Stop the development host, then run:

```bash
ew inspect . --format json --output ./artifacts/resolved-module.json
ew pack . --configuration Release --output ./artifacts
ew verify ./artifacts/acme.leave/1.0.0
```

The output directory uses the **business module release `1.0.0`**, not the framework package version. `pack` refuses to overwrite an existing artifact. Choose a fresh output directory when repeating the tutorial unchanged.

Continue with [project anatomy](../../basics/project-anatomy/) or [LeaveRequest](../../examples/leave-request/).

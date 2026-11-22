---
title: "Custom CLI distributions"
description: "Build an organization CLI, shared services, templates and a branded kernel."
---

A distribution gives your organization a coherent kernel, developer tool, SDK, shared contracts and project templates. Use the repository-source track for the APIs shown here; publishing a new NuGet release is a separate operation.

## Follow the complete tutorial

Start with [Build your organization’s workflow platform](../../organization/). The tutorial includes project trees, source-derived code panels, shell commands, expected results and failure exercises.

| Task | Chapter |
| --- | --- |
| Separate contracts, SDK, hosting and modules | [Solution](../../organization/solution/) |
| Share services between CLI and kernel | [Kernel](../../organization/kernel/) |
| Name your CLI and distribute templates | [CLI](../../organization/cli/) |
| Write and exercise the business workflow | [Module](../../organization/module/) |
| Connect authentication and identities | [Identities](../../organization/identity/) |
| Add second authentication | [Second factor](../../organization/second-factor/) |
| Find the remaining extension points | [Extension catalog](../../organization/extensions/) |
| Install the same artifact in your kernel | [Delivery](../../organization/delivery/) |

## Composition rules

Standard CLI commands are included. `AddCommand` supports compiled commands and subcommands with declared options; `ReplaceCommand` is explicit. Internal development commands and authentication services remain reserved. Development continues to use test identities from `ew.dev.json`.

The host registers shared contract assemblies and its complete runtime identity/version. Modules declare one runtime requirement using NuGet version ranges. Test the supported range; do not infer compatibility from matching service names.

Run your tool’s `setup` explicitly before `new`. Keep the template’s SDK version fixed and aligned with your distribution. Configure feed credentials through ordinary NuGet mechanisms.

Use `ConfigureBranding` for name, resources and palette. Custom hosts lock administrative fields by default; `EditableFields` explicitly permits selected fields. Server writes enforce that policy.

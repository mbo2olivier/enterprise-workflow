---
title: "Build and extend modules"
description: "Declare ownership once and retain exact dependencies."
sidebar:
  order: 2
---

A fluent module derives from `WorkflowModule`, supplies `Info`, declares permissions and configures forms, handlers and processes. The [generated template](../../basics/project-anatomy/) supplies a working entry point.

Register reusable forms with `AddForm`, then refer to them from processes in the same module. Register a handler once; missing or ambiguous registrations fail resolution. Module-local IDs are qualified with the module ID so that unrelated modules can use readable local names.

The public builder supports references to retained contracts through `UseHandler`, `UseForm` and `UseProcess`. These APIs preserve the original artifact owner. They do not copy an old definition into a new owner's identity or permit replacing a handler underneath existing instances.

When using such references, pass all required exact artifacts to validation, packaging and the target installation. `--module-directory` supplies retained owners to the CLI. The package's own dependency declarations are checked against that catalogue.

File forms must be declared in the process module with their media policies and validators. Cross-artifact importing of a file form is rejected. Resource and component declarations remain hashed with their artifacts.

Finish with `ew validate`, `ew inspect`, `ew pack` and `ew verify`. See [loading and isolation limits](../../advanced-topics/modules/) before accepting third-party module code.

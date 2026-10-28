---
title: "Install the Kernel"
description: "Select a release archive, initialize the database and provision administration."
sidebar:
  order: 1
---

Download the archive for your target from [release 1.0.0-rc.1](https://github.com/mbo2olivier/enterprise-workflow/releases/tag/v1.0.0-rc.1). Available RIDs are `linux-x64`, `win-x64` and `osx-arm64`, each as framework-dependent or self-contained. Verify the archive against the release's `SHA256SUMS` before extraction.

A framework-dependent archive needs ASP.NET Core 10. A self-contained archive includes its runtime and must be replaced when runtime security updates are needed. Native OS prerequisites still apply. No trimming or Native AOT is claimed.

## Initialize a local installation

Extract to a dedicated directory. Run from that directory so that accompanying settings and resources are present. For a framework-dependent distribution:

```bash
dotnet EnterpriseWorkflow.Kernel.dll migrate
dotnet EnterpriseWorkflow.Kernel.dll bootstrap administrator '<choose-a-unique-strong-password>'
dotnet EnterpriseWorkflow.Kernel.dll
```

Replace the password placeholder locally. The bootstrap CLI accepts it as an argument, so use a controlled administrative session and account for command history/process visibility. Do not commit or publish the value. Bootstrap is an explicit initial provisioning operation, not an always-open HTTP setup endpoint. Check its printed outcome; an already provisioned installation must be administered through its security workflow.

For a self-contained archive invoke `./EnterpriseWorkflow.Kernel` (or `EnterpriseWorkflow.Kernel.exe` on Windows) with the same commands. The default SQLite file is under the application's base directory; set a durable connection path before initialization when deploying under a service account.

## Configure and check

Set listening addresses through the normal ASP.NET environment configuration, such as `ASPNETCORE_URLS`. Protect production HTTP with the installation's HTTPS arrangement. Set the module catalogue and media root using the [Kernel configuration reference](../../reference/kernel-configuration/).

Log in, create appropriate identities/profiles and grant only the intended business permissions. The administrator has no automatic right to approve LeaveRequest tasks.

Verify login, catalogue visibility, starting an authorized instance, a waiting task across restart and its completion. Keep backups and rollback plans before changing schema or module inventory. See [database setup](../databases/) and [module deployment](../modules/).

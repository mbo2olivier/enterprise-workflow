---
title: "Public package catalogue"
description: "The 20 published packages in release 1.0.0-rc.1."
sidebar:
  order: 0
---

All packages below are published as **1.0.0-rc.1**. Choose the top-level integration you need and let NuGet restore its dependency graph. The generated module uses Extensions.Abstractions; the embedded template supplies the runtime and SQLite/security integration references.

| Package | Purpose |
| --- | --- |
| [EnterpriseWorkflow.Abstractions](https://www.nuget.org/packages/EnterpriseWorkflow.Abstractions/1.0.0-rc.1) | Shared business primitives and annotations |
| [EnterpriseWorkflow.Cli](https://www.nuget.org/packages/EnterpriseWorkflow.Cli/1.0.0-rc.1) | The ew .NET tool |
| [EnterpriseWorkflow.Core](https://www.nuget.org/packages/EnterpriseWorkflow.Core/1.0.0-rc.1) | Canonical definitions, state and serialization |
| [EnterpriseWorkflow.Extensions.Abstractions](https://www.nuget.org/packages/EnterpriseWorkflow.Extensions.Abstractions/1.0.0-rc.1) | Fluent module authoring entry point |
| [EnterpriseWorkflow.Extensions](https://www.nuget.org/packages/EnterpriseWorkflow.Extensions/1.0.0-rc.1) | Module catalogue and host integration |
| [EnterpriseWorkflow.Persistence.Abstractions](https://www.nuget.org/packages/EnterpriseWorkflow.Persistence.Abstractions/1.0.0-rc.1) | Workflow store and maintenance contracts |
| [EnterpriseWorkflow.Persistence.Oracle](https://www.nuget.org/packages/EnterpriseWorkflow.Persistence.Oracle/1.0.0-rc.1) | Oracle workflow store and migrations |
| [EnterpriseWorkflow.Persistence.Sqlite](https://www.nuget.org/packages/EnterpriseWorkflow.Persistence.Sqlite/1.0.0-rc.1) | SQLite workflow store and migrations |
| [EnterpriseWorkflow.Presentation.Abstractions](https://www.nuget.org/packages/EnterpriseWorkflow.Presentation.Abstractions/1.0.0-rc.1) | Forms, portal and UI contracts |
| [EnterpriseWorkflow.Presentation](https://www.nuget.org/packages/EnterpriseWorkflow.Presentation/1.0.0-rc.1) | Authorized portal services, reports and notifications |
| [EnterpriseWorkflow.Runtime.Abstractions](https://www.nuget.org/packages/EnterpriseWorkflow.Runtime.Abstractions/1.0.0-rc.1) | Handlers, execution and media contracts |
| [EnterpriseWorkflow.Runtime](https://www.nuget.org/packages/EnterpriseWorkflow.Runtime/1.0.0-rc.1) | Durable workers, execution, outbox and media services |
| [EnterpriseWorkflow.Sdk](https://www.nuget.org/packages/EnterpriseWorkflow.Sdk/1.0.0-rc.1) | Fluent process/form/module builders |
| [EnterpriseWorkflow.Security.Abstractions](https://www.nuget.org/packages/EnterpriseWorkflow.Security.Abstractions/1.0.0-rc.1) | Identity, directory and authorization contracts |
| [EnterpriseWorkflow.Security.Persistence.Oracle](https://www.nuget.org/packages/EnterpriseWorkflow.Security.Persistence.Oracle/1.0.0-rc.1) | Oracle security store |
| [EnterpriseWorkflow.Security.Persistence.Sqlite](https://www.nuget.org/packages/EnterpriseWorkflow.Security.Persistence.Sqlite/1.0.0-rc.1) | SQLite security store |
| [EnterpriseWorkflow.Security.Persistence](https://www.nuget.org/packages/EnterpriseWorkflow.Security.Persistence/1.0.0-rc.1) | Shared security persistence contracts and migrations |
| [EnterpriseWorkflow.Security](https://www.nuget.org/packages/EnterpriseWorkflow.Security/1.0.0-rc.1) | Local/directory security services |
| [EnterpriseWorkflow.Web](https://www.nuget.org/packages/EnterpriseWorkflow.Web/1.0.0-rc.1) | Web UI and HTTP integration |
| [EnterpriseWorkflow.Templates](https://www.nuget.org/packages/EnterpriseWorkflow.Templates/1.0.0-rc.1) | ew-module and ew-embedded project templates |

The Kernel is distributed as release archives, not as another public package in this catalogue. Tests, fixtures and business samples are not public NuGet packages.

Use explicit versions for reproducible applications. A framework upgrade does not automatically change your module's business release or migrate its state. Review [compatibility](../compatibility/) and [versioning](../../advanced-topics/versioning/) before upgrading.

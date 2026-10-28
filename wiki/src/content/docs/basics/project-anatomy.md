---
title: "Project anatomy"
description: "Understand the files created by ew-module."
sidebar:
  order: 1
---

`dotnet new ew-module --name Acme.Leave --module-id acme.leave` creates a Razor class library. Keep the generated project as the smallest compilable reference when learning the DSL.

| File or directory | Purpose |
| --- | --- |
| C# module source | Module identity, permissions, forms, process, DTO and handlers |
| `.csproj` | .NET target, ASP.NET framework reference and public NuGet dependency |
| `ew.dev.json` | Local identities, permissions and development settings |
| `packages.lock.json` | Resolved package graph after restore |
| `.ew/` | Ignored local build, retained artifacts and environment data |
| `artifacts/` | An output location you select for production packaging |

`WorkflowModule.Info` owns the module ID, release and contract revision. `ConfigurePermissions` declares local permission IDs. `Configure` registers forms and handlers and adds process classes. Each `IWorkflowProcess` configures a typed process builder.

The template keeps these types in one source file for readability. Split them into folders as your module grows, preserving exactly one public concrete `WorkflowModule` entry point in the project's target assembly.

Do not commit `.ew/` or distribute its development generations. The reproducible production boundary is the verified artifact emitted by `ew pack`. See [packaging](../packaging/).

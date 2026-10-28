---
title: "Install CLI, templates and packages"
description: "Install the published 1.0.0-rc.1 toolchain from NuGet."
sidebar:
  order: 2
---

The commands in this wiki pin the public release candidate **1.0.0-rc.1**. Framework package versions and business module releases are separate numbers.

```bash
dotnet tool install --global EnterpriseWorkflow.Cli --version 1.0.0-rc.1
dotnet new install EnterpriseWorkflow.Templates@1.0.0-rc.1 --nuget-source https://api.nuget.org/v3/index.json
ew --help
dotnet new list ew-
```

If `ew` is not found, add the .NET global tools directory to PATH and open a new shell. On Unix it is normally `$HOME/.dotnet/tools`; on Windows it is normally `%USERPROFILE%\.dotnet\tools`.

For an existing installation, use `dotnet tool update --global EnterpriseWorkflow.Cli --version 1.0.0-rc.1`. Installing the template package again selects that template version. Do not assume `ew --version` shows the NuGet prerelease suffix: it reports the assembly version. `dotnet tool list --global` shows the installed tool package version.

## Library consumption

The module template already references `EnterpriseWorkflow.Extensions.Abstractions`. The embedded template supplies its own runtime/provider references. To add a specific library to another project:

```bash
dotnet add package EnterpriseWorkflow.Runtime --version 1.0.0-rc.1
```

Use the [package catalogue](../../reference/packages/) to select the correct contract or adapter. NuGet must be enabled in your package sources; private source mappings must allow `EnterpriseWorkflow.*` from NuGet.org. These tutorials do not require a local package feed or a project reference to the framework repository.

Next: [run the generated approval process](../first-workflow/).

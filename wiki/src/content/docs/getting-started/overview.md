---
title: "Overview & prerequisites"
description: "Choose modular or embedded hosting and prepare your workstation."
sidebar:
  order: 1
---

Enterprise Workflow separates the engine, the business module and the application that hosts them. A module declares processes, permissions, handlers, forms and its own immutable release identity. The Kernel supplies a modular web application; embedded hosting lets you compose those services yourself.

| Mode | Best starting point | Your responsibility |
| --- | --- | --- |
| Module + Windows Kernel | Start with the precompiled executable, then deploy an SDK-built module | Module source, installation, identities and deployment |
| Module + `ew dev` | Design and test the module before packaging for the Kernel | C# module and explicit demo permissions |
| Embedded | Integrate into an existing application | Composition, authentication, authorization, workers and UI |

## Workstation prerequisites

For the first tutorial, obtain the precompiled **win-x64 self-contained Kernel archive** through your software distribution channel. Install the .NET **10.0.401 SDK** on the developer workstation and verify `dotnet --version`. Keep `ew` available on your PATH. The tutorial uses SQLite and a local HTTP loopback address. You do not need framework sources, Oracle, LDAP, Docker or an SMTP service.

The generated modules use Razor and reference the ASP.NET Core shared framework. A framework-dependent Kernel archive needs ASP.NET Core 10 on its host; a self-contained archive bundles the runtime but still needs the platform's native prerequisites.

The documentation site has a separate Node toolchain; it is not required to use the .NET framework. Archive targets and adapter qualification are listed in [compatibility](../../reference/compatibility/).

Continue with [installation](../install/), then [your first workflow](../first-workflow/).

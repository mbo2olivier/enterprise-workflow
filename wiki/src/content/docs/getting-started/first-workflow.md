---
title: "Your first workflow"
description: "Start an empty Windows Kernel, build a module with the SDK, deploy it and complete an approval."
sidebar:
  order: 3
---

This walkthrough uses the **precompiled Windows Kernel**, with a separate SDK project. You develop and test the business module with `ew dev`, then deploy its packaged artifact to the compiled runtime. You do not build the framework. Use PowerShell and a disposable local installation. The same executable will first show an empty catalogue, then run your packaged process after a restart.

## 1. Start the empty runtime

Extract the supplied **win-x64 self-contained Kernel archive** into `C:\EnterpriseWorkflow\Runtime`. Keep all archive files together. Start with a new SQLite database and ensure `appsettings.json` contains an empty catalogue, preserving its other settings:

```json
"Modules": { "Directories": [] }
```

Initialize the installation once and start the executable:

```powershell
Set-Location C:\EnterpriseWorkflow\Runtime
.\EnterpriseWorkflow.Kernel.exe migrate
.\EnterpriseWorkflow.Kernel.exe bootstrap administrator '<choose-a-unique-strong-password>'
$env:ASPNETCORE_URLS = 'http://localhost:5100'
.\EnterpriseWorkflow.Kernel.exe
```

Replace the password locally. Bootstrap receives it as a command argument; use a controlled session and account for command history/process visibility. Check that bootstrap succeeds. A self-contained archive needs no separate .NET runtime; the developer workstation still needs the .NET SDK for module compilation.

Open `http://localhost:5100`, sign in as the administrator and inspect the installation: no business module is configured. A user's empty process list alone is not proof that no module is installed, because visibility also depends on permissions.

## 2. Create a process with the SDK

In a second PowerShell window, after [installing the CLI and templates](../install/):

```powershell
New-Item -ItemType Directory -Force C:\EnterpriseWorkflow\Projects | Out-Null
Set-Location C:\EnterpriseWorkflow\Projects
dotnet new ew-module --name Acme.Leave --module-id acme.leave
Set-Location Acme.Leave
dotnet restore
ew validate .
ew inspect .
ew dev . --environment tutorial --url http://localhost:5101
```

Open the generated C# module to review its typed state, form, approval and closing service. Adjust business labels or rules before packaging. This starter is a simple requester-to-reviewer approval; the complete three-actor example is [LeaveRequest](../../examples/leave-request/).

The artifact uses the **business module release `1.0.0`**, independently of framework version `1.0.0-rc.1`. `pack` does not overwrite an existing artifact. Use a fresh output directory when repeating an unchanged build, or deliberately increment release/revision identifiers for a changed module.

## 3. Design and test with `ew dev`

The development host runs separately from the Windows Kernel, at `http://localhost:5101`. It builds the module, maintains an isolated SQLite environment and watches source changes. The Kernel remains at `http://localhost:5100`, with no business module configured.

In the development UI, select **Alice**, create a request and submit a reason of 3–200 characters. Select **Bob**, claim the approval and approve it. Repeat with a rejection and its required comment. Edit the C# form, business rules or labels and exercise the flow again; source rebuilds create retained local generations for existing instances.

The identity selector and seeded permissions come from `ew.dev.json`. They belong to the development environment. They are not accounts in the separate Kernel installation.

Stop `ew dev` with Ctrl+C before packaging the declared business release:

```powershell
ew pack . --configuration Release --output .\artifacts
ew verify .\artifacts\acme.leave\1.0.0
```

## 4. Deploy the module and restart the runtime

Stop the Windows Kernel in the first PowerShell window with Ctrl+C. From the module project directory, copy the **whole artifact**, including its manifest, assemblies, dependencies and resources:

```powershell
New-Item -ItemType Directory -Force C:\EnterpriseWorkflow\Modules\acme.leave | Out-Null
Copy-Item -Recurse .\artifacts\acme.leave\1.0.0 C:\EnterpriseWorkflow\Modules\acme.leave\1.0.0
```

The destination must not already exist. In `C:\EnterpriseWorkflow\Runtime\appsettings.json`, replace the empty catalogue with:

```json
"Modules": {
  "Directories": ["C:\\EnterpriseWorkflow\\Modules\\acme.leave\\1.0.0"]
}
```

Deploy only the packaged artifact, not the `.ew` directory, development database or `ew.dev.json` identities. This is a fragment of the existing configuration, not a replacement for the whole file. Register the exact version directory; the Kernel does not discover packages recursively or hot-load copied DLLs. Restart in the first window:

```powershell
.\EnterpriseWorkflow.Kernel.exe
```

Check startup diagnostics before signing in. No framework rebuild is required.

## 5. Provision users and complete the process

In **Administration > Access**, create local accounts Alice and Bob with individual passwords. Create profiles, assign the following module permissions, then associate the accounts with their profiles:

| Account | Permissions |
| --- | --- |
| Alice, requester | `acme.leave.request`, `acme.leave.read` |
| Bob, reviewer | `acme.leave.review`, `acme.leave.read` |

The Kernel does **not** import `ew.dev.json` demo users and has no development identity selector. Sign out and sign in as each actor, or use separate browser sessions. The administrator has no implicit business approval rights.

1. Sign in as Alice. Start the process, enter a reason of 3–200 characters and submit.
2. Sign in as Bob. Find the approval in his inbox, claim it and approve it, optionally adding a comment.
3. The closing handler updates the state and the process reaches its accepted outcome.
4. Inspect the completed instance, comments and progression as an authorized reader.
5. Repeat and decline a new request; a rejection comment is required. Alice cannot approve her own request by default.

The starter's labels include **Motif**, **Approuver** and **Refuser**. To demonstrate persistence, leave another request waiting, stop and restart the same executable with the same database/catalogue, then complete the waiting task.

For production operations, follow [module deployment](../../operations/modules/), including backups, retained artifact versions, access review and release approval. For the three-actor demonstration, continue with [LeaveRequest](../../examples/leave-request/).

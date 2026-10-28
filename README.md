# Enterprise Workflow

Build durable business workflows in C# and .NET 10. Enterprise Workflow combines a fluent SDK, typed business state, human approvals, service handlers and persistent execution with a modular web host or embedded application hosting.

**Public release:** [1.0.0-rc.1](https://github.com/mbo2olivier/enterprise-workflow/releases/tag/v1.0.0-rc.1) · **License:** [Apache-2.0](LICENSE)

## Documentation

Visit the [Enterprise Workflow wiki](https://mbo2olivier.github.io/enterprise-workflow/) for tutorials, concepts, integration guides, deployment procedures and reference material.

- [Get started](https://mbo2olivier.github.io/enterprise-workflow/getting-started/overview/) — prerequisites, installation and your first workflow.
- [Follow LeaveRequest](https://mbo2olivier.github.io/enterprise-workflow/examples/leave-request/) — requester confirmation, HR review, manager assignment, attachments and reporting.
- [Embed the engine](https://mbo2olivier.github.io/enterprise-workflow/getting-started/embedded/) — compose the runtime inside your application.
- [Deploy the Kernel](https://mbo2olivier.github.io/enterprise-workflow/operations/install/) — install, configure, back up and maintain the modular host.
- [Browse packages and CLI reference](https://mbo2olivier.github.io/enterprise-workflow/reference/packages/).

## Quick start

Install the .NET 10 SDK, then the published CLI and templates:

```bash
dotnet tool install --global EnterpriseWorkflow.Cli --version 1.0.0-rc.1
dotnet new install EnterpriseWorkflow.Templates@1.0.0-rc.1 --nuget-source https://api.nuget.org/v3/index.json
dotnet new ew-module --name Acme.Leave --module-id acme.leave
cd Acme.Leave
ew dev .
```

Open the URL printed by `ew dev`. Select Alice to create a request, then Bob to claim and approve it. See the [first workflow tutorial](https://mbo2olivier.github.io/enterprise-workflow/getting-started/first-workflow/) for the complete walkthrough and expected results.

## Capabilities

- Sequential process graphs with exclusive decisions and durable waiting/recovery.
- Typed state, explicit validation, versioned handlers and immutable module artifacts.
- Human tasks, forms, assignment rules, permission checks and approval comments.
- Private attachments and generated documents, authorized search, audit exports and calendar TAT reports.
- SQLite and Oracle adapters, local identity and LDAP integration.
- Modular Kernel archives and embedded application composition.

This release is a prerelease. Loops, parallel branches and the visual Studio are outside its scope. Provider qualification is specific to the tested environments; see [compatibility and limits](https://mbo2olivier.github.io/enterprise-workflow/reference/compatibility/).

## Build from source

The repository pins .NET SDK `10.0.401`. From its root:

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore
dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
```

To develop the independent documentation project, use Node.js 22.23.4 or newer:

```bash
cd wiki
npm ci
npm run build
npm run dev
```

The wiki is authored in English under `wiki/`. The repository's `docs/` directory remains the working area for maintainers and coding agents: specifications, architecture decisions, plans and qualification evidence. It stays versioned and is not imported or published by the public wiki build.

Report issues through [GitHub Issues](https://github.com/mbo2olivier/enterprise-workflow/issues). Third-party dependencies retain their own licenses.

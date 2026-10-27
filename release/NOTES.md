# Enterprise Workflow 1.0.0-rc.1

First public release candidate for the .NET 10 workflow framework: durable
execution, typed business state, fluent SDK, authorization, audit, media and TAT
reports. Includes the `ew` CLI and `ew-module` / `ew-embedded` templates.

NuGet owner: `mukadi.sheba`. License: Apache-2.0; dependencies retain their licenses.

Kernel archives target Windows x64, Ubuntu Linux x64 and macOS ARM64, with both
self-contained and framework-dependent variants. Framework-dependent archives
require ASP.NET Core 10. Self-contained archives still require the OS native
prerequisites and must be rebuilt for runtime security updates. No trimming/AOT.

SQLite archive smoke checks include module loading, migrations, HTTP startup and
backup/restoration. Oracle support evidence remains specific to Oracle Enterprise
19.19 and the environments documented in the repository; no broader OS/provider
matrix is implied. No throughput, concurrency, latency, SLA or RPO/RTO guarantees
are claimed. Deployment sizing and operational objectives belong to each adopter.

Published package versions and hashes are listed in the attached manifest.
This is a prerelease; public API and distribution feedback is welcome.

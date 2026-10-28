---
title: "Compatibility and limits"
description: "Published archive targets and adapter-specific qualification."
sidebar:
  order: 8
---

The public release is **1.0.0-rc.1**, a prerelease. The .NET SDK used for build/qualification is **10.0.401**. See [release notes and artifacts](https://github.com/mbo2olivier/enterprise-workflow/releases/tag/v1.0.0-rc.1).

| Archive RID | Release target | Variants |
| --- | --- | --- |
| `linux-x64` | Ubuntu Linux x64 | Framework-dependent and self-contained |
| `win-x64` | Windows x64 | Framework-dependent and self-contained |
| `osx-arm64` | macOS Apple Silicon | Framework-dependent and self-contained |

Qualification runners are Ubuntu 24.04, Windows 2025 and macOS 15. Archive SQLite smoke checks cover module load, migrations, HTTP startup and backup/restoration. A RID alone does not establish every provider's support.

Oracle evidence is specific to **Oracle Enterprise 19.19** and the qualified environments. It is not a claim that every Oracle version or OS/provider combination was exercised. OpenLDAP/LDAPS qualification runs on Linux; Active Directory, StartTLS and nested groups are not claimed.

Framework-dependent archives require ASP.NET Core 10. Self-contained archives retain OS native requirements and require rebuild/replacement for embedded runtime updates. There is no trimming/Native AOT guarantee. Other RIDs, including x86, Linux ARM64/musl and macOS Intel, are not published archive targets of this release.

## Functional boundaries

- Sequential graphs and exclusive decisions; no loops or parallel branches.
- No delivered visual Studio.
- No hostile-code sandbox or hot-unload guarantee for modules.
- CLI packaging rejects native/RID-specific and satellite module dependencies.
- No automatic migration chains or arbitrary failed-instance state editor.
- TAT is elapsed calendar time, not business-hours or active-effort measurement.
- No throughput, latency, concurrency, availability, SLA or RPO/RTO guarantees.

Choose deployment objectives for your own installation and test its topology, integrations and recovery procedure.

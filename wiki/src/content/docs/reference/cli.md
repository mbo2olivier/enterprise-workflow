---
title: "CLI reference"
description: "Commands, options and expected outputs for ew."
sidebar:
  order: 1
---

`EnterpriseWorkflow.Cli` installs the `ew` command. All commands below target public version `1.0.0-rc.1`.

| Command | Input | Result |
| --- | --- | --- |
| `ew validate .` | Module project or directory | Builds and validates declarations/bindings |
| `ew inspect .` | Module project | Human-readable resolved graph |
| `ew inspect . --format json` | Module project | Resolved JSON on stdout |
| `ew pack . --output ./artifacts` | Module project | Verified immutable artifact directory |
| `ew verify <artifact-directory>` | Packaged artifact | Read-only integrity/catalogue validation |
| `ew dev .` | Sources | Local UI, SQLite, watch and retained generations |
| `ew dev --artifact <directory>` | Production artifact | Local execution without source build/watch |

## Options

| Option | Scope | Meaning |
| --- | --- | --- |
| `--project <path>` | Source commands | Select `.csproj` or directory explicitly |
| `--configuration Debug\|Release` | Source commands | Build configuration; default Debug |
| `--output <path>` | inspect, pack | Inspection file or artifact output root |
| `--format text\|json` | inspect | Output format |
| `--module-directory <path>` | Catalogue-aware commands | Retained exact owner; repeatable |
| `--config <path>` | dev | Development JSON file |
| `--environment <name>` | dev | Select named dataset/settings |
| `--url <url>` | dev | HTTP loopback root URL with a port |
| `--no-watch` | dev sources | Disable source watch |
| `--reset` | dev | Archive/recreate owned selected environment |
| `--artifact <path>` | dev | Run an exact production artifact |

Without a project argument, source commands search the current directory. Multiple projects require explicit selection. Artifact mode refuses a project argument, `--configuration` or `--no-watch`. `verify` and `validate` refuse `--output`.

Build diagnostics go to stderr so inspection JSON can be consumed independently. Success returns zero; managed CLI diagnostics return code 1, and failures return nonzero. A package path is announced only after successful validation. `ew --help` shows the compact usage and `ew --version` shows assembly version, which is not the full NuGet prerelease identity.

For worked commands see [packaging](../../basics/packaging/) and [the local cycle](../../basics/development-cycle/). Internal `__...` commands are supervisor implementation details, not a public authoring interface.

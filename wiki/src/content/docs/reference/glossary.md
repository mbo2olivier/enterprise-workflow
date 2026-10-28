---
title: "Glossary and project links"
description: "A compact vocabulary for the engine and module lifecycle."
sidebar:
  order: 9
---

| Term | Meaning |
| --- | --- |
| Definition | Immutable versioned process graph |
| Instance | One durable execution of a definition |
| Activation | Persisted execution work for a node |
| Artifact | Hashed immutable module package with its code and resources |
| Module release | Business artifact version, separate from framework NuGet version |
| Contract revision | Version of redeclared module-owned contracts |
| State schema | Explicit versioned DTO contract for business data |
| Handler | Code bound to a stable, versioned reference |
| Lease / fencing | Attempt ownership and stale-commit protection |
| Outbox | Durable external-delivery intent and its retry lifecycle |
| Pool task | Eligible task requiring claim by a participant |
| Projection | Server-filtered view of state/data for an actor |
| TAT | Calendar turnaround time derived from durable observations |
| Development generation | Locally retained source build, not a production release |

## Project resources

- [Source repository](https://github.com/mbo2olivier/enterprise-workflow)
- [Release 1.0.0-rc.1](https://github.com/mbo2olivier/enterprise-workflow/releases/tag/v1.0.0-rc.1)
- [NuGet package catalogue](../packages/)
- [Issue tracker](https://github.com/mbo2olivier/enterprise-workflow/issues)
- [Apache-2.0 license](https://github.com/mbo2olivier/enterprise-workflow/blob/main/LICENSE)

The public wiki is maintained with the source so examples and contracts can change together. Internal design notes, ADRs, implementation planning and qualification work remain in the repository's `docs/` directory for maintainers and coding agents. Public readers do not need that internal reading path to follow these tutorials.

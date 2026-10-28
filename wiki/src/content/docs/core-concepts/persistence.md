---
title: "Persistence and presentation"
description: "Keep engine data, UI projections and binary storage coherent."
sidebar:
  order: 7
---

The workflow store persists definitions, instances, activations, tasks, outbox work and audit/TAT records. SQLite and Oracle implement the persistence contracts. Security persistence is a separate set of contracts and migrations even when both stores use the same database connection.

Apply migrations explicitly before accepting work. A store constructor does not silently create or upgrade a schema. The Kernel defaults to schema validation; `migrate` applies both workflow and security migrations.

Business DTOs drive state projection. Form definitions drive submissions and validation. Neither is a direct database-table editor. The UI receives filtered state and authorized operations; hiding a control in a custom component is not a replacement for server checks.

Private media binaries live outside the static web root, with attachment metadata committed through the workflow protocol. Backups therefore include database, media volume, referenced immutable artifacts and compatible configuration.

SQLite is useful for local discovery and suitable installations; Oracle needs a configured connection and properly qualified server/provider combination. The release does not imply universal equivalence across every platform or database version. See [database setup](../../operations/databases/), [backup/restore](../../operations/backup/) and [compatibility](../../reference/compatibility/).

---
title: "SQLite and Oracle setup"
description: "Select a database and apply migrations explicitly."
sidebar:
  order: 2
---

The Kernel selects `Sqlite` or `Oracle` with `Database:Provider`. `ConnectionStrings:EnterpriseWorkflow` supplies the provider's connection string. Keep credentials outside tracked settings.

SQLite defaults to a local `workflow.db` under the application base directory. Set an explicit durable path when the application directory will be replaced during updates. Ensure the service account can access the database and its journal files.

Oracle requires a connection string and a migration account with the DDL rights needed by both workflow and security schemas. Qualification is specific to Oracle Enterprise **19.19** and the recorded environments; the release does not assert every Oracle/OS combination.

## Migration lifecycle

`Database:MigrationMode` defaults to `Validate`. Startup checks both workflow and security migrations and refuses a schema that is not ready. Run the Kernel's `migrate` command to apply pending migrations explicitly; an operator may also select `Apply` deliberately during controlled setup.

In an embedded host, construct `SqliteWorkflowDatabase`/`SqliteSecurityDatabase` or the corresponding Oracle objects and call `MigrateAsync` before composing executing workers. A store does not migrate automatically.

## Before a schema update

Back up the database, artifacts and media coherently. Apply the migration in an isolated restored installation first. Check the new schema and business paths before starting production workers. An older executable is not automatically compatible with a migrated schema; returning to an old binary alone is not a validated rollback.

Keep direct SQL administration separate from runtime permissions. A reporting user should be read-only. Do not repair leases, states or assignments by arbitrary SQL writes; use tested engine protocols and maintenance contracts.

See [backup and restore](../backup/) and [reporting](../../reference/reporting/).

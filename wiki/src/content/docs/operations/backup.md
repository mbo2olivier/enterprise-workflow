---
title: "Backup, restore and retention"
description: "Preserve one coherent database, media and artifact set."
sidebar:
  order: 4
---

A usable backup contains the database, private media objects, every referenced immutable module artifact, compatible application/configuration versions and access to the deployment secrets needed for recovery. Backing up only SQLite or only the application directory is insufficient.

## Capture a coherent set

Stop host writes and uploads, then use the database's supported backup tools. For SQLite use its backup API or a procedure accounting for WAL and checkpoint behavior; a raw copy of the active database file is not a qualified backup. Copy the private media volume and referenced artifacts at the same operational consistency point. Restart only after the capture is complete.

For Oracle, coordinate the database backup and restoration point with the actual database operations team. Qualify that procedure with your storage topology; an archive's SQLite smoke test does not prove an Oracle backup method.

## Restore and demonstrate recovery

1. Restore into an isolated environment with outbound integrations held inactive.
2. Restore database, media and exact module artifacts together.
3. Restore compatible configuration and protected secret access.
4. Check migrations, catalogue ownership, identities, waiting tasks and outbox state.
5. Start the host and complete a previously waiting instance using its exact old code.
6. Read an authorized attachment and inspect audit/TAT continuity.
7. Re-enable external delivery only after duplicate-effect handling has been considered.

Messages delivered after the backup point can be delivered again after recovery. Recipient idempotency remains required.

## Retention

Logical media deletion removes access to revisions; it does not promise immediate physical erasure. Staging purge is explicit and bounded. Keep an installation retention policy for database history, files and artifacts before deleting them. Do not delete an artifact still referenced by durable instances or historical presentation.

No RPO/RTO, availability or capacity guarantee is claimed by this release. Define and test your own recovery objectives.

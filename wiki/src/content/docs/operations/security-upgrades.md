---
title: "Secure and update an installation"
description: "Prepare credentials, boundaries and rollback before rollout."
sidebar:
  order: 6
---

Use a dedicated service identity, protected configuration/secret injection and least-privilege access to databases and private media. Keep `Media:Root` outside static content. Terminate HTTPS according to your deployment design and use trusted TLS for external directory and SMTP connections.

Module code executes inside the host's trust boundary. Review and control its source and dependencies before installing it. Manifest hashes establish identity and integrity; they do not isolate hostile code.

## Upgrade checklist

- Read the target release notes and retain the exact current release artifacts and configuration.
- Review framework, module contract and state-schema changes independently.
- Back up database, media and module inventory coherently.
- Verify migration and old-instance resumption in an isolated restored environment.
- Update runtime/provider packages and archive together according to their compatibility.
- Roll out under a controlled stop/start procedure and exercise a business path.
- Preserve old owners until no required durable references remain.

A framework-dependent deployment receives runtime patches through the installed ASP.NET runtime. A self-contained archive embeds its runtime and needs a rebuilt/replaced archive for runtime fixes. Neither distribution removes the need to update native OS dependencies.

If a schema migration has occurred, do not assume replacing binaries with the previous version is a rollback. Restore a coherent backup or use an explicitly tested reverse migration. See [backup/restore](../backup/).

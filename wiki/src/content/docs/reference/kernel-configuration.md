---
title: "Kernel configuration reference"
description: "Settings consumed by the packaged modular host."
sidebar:
  order: 3
---

The Kernel loads `appsettings.json` and `appsettings.<environment>.json` from its application base directory, then environment configuration. Nested environment keys use `__`, for example `Database__Provider=Sqlite`. The environment comes from `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT`, defaulting to Production.

| Key | Purpose / default |
| --- | --- |
| `Database:Provider` | `Sqlite` or `Oracle`; default Sqlite |
| `Database:MigrationMode` | `Validate` or `Apply`; default Validate |
| `ConnectionStrings:EnterpriseWorkflow` | Database connection; required for Oracle |
| `Installation:Id` | Installation identity; default enterprise-workflow |
| `Modules:Directories` | Array of exact artifact directories |
| `Media:Root` | Durable private media directory; default application-base/media |
| `Security:AuthenticationProvider` | local or the configured LDAP provider ID |
| `Security:Ldap` | Directory options and service credentials |
| `Security:Policy` | Session, password, lockout and request-size policy |
| `Notifications:Smtp` | Mail transport settings |
| `Workflow:Serialization` | `MaximumStateBytes` and `MaximumDepth` |

Example configuration fragment with paths to replace for your host:

```json
{
  "Database": { "Provider": "Sqlite", "MigrationMode": "Validate" },
  "ConnectionStrings": { "EnterpriseWorkflow": "Data Source=/srv/enterprise-workflow/data/workflow.db" },
  "Installation": { "Id": "acme-workflow" },
  "Modules": { "Directories": ["/srv/enterprise-workflow/modules/acme.leave/1.0.0"] },
  "Media": { "Root": "/srv/enterprise-workflow/media" }
}
```

This is Kernel JSON, not an `ew.dev.json` document. Windows installations need valid JSON-escaped Windows paths or equivalent environment configuration. Keep data paths stable across application replacement.

The [LdapDirectoryOptions contract](https://github.com/mbo2olivier/enterprise-workflow/blob/v1.0.0-rc.1/src/EnterpriseWorkflow.Security/SecurityOptions.cs) defines transport, base DN, filters and attribute codecs. The Kernel additionally requires `ServiceUserName` and `ServicePassword` when LDAP is enabled. Defaults include port 636, LDAPS, `uid` login, `entryUUID` subject and a 10-second timeout; set values for your actual directory.

SMTP options include Host, Port, EnableSsl, UserName, Password, From, BaseUrl, PageSize, TimeoutSeconds and Addresses. See the [SMTP guide](../../guides/notifications/). The Kernel binds only the documented serialization subsection of runtime options; broader worker tuning is performed through a custom composition root.

Migrate and bootstrap before starting a new installation. See [installation](../../operations/install/).

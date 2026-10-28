---
title: "Identity, permissions and LDAP"
description: "Configure local or directory identities without coupling modules to groups."
sidebar:
  order: 4
---

Declare module permissions with `ConfigurePermissions`, then require those local IDs on start/actions/tasks. A module does not hard-code an installation's profile names, LDAP group names or account passwords.

The Kernel supports local authentication and a configured LDAP directory provider. Profiles in the installation map permissions to provider-qualified identities or directory groups. Stable subjects survive display-name changes; use the configured unique directory attribute rather than email as the identity key.

For a first local installation, apply migrations and use the [explicit bootstrap command](../../operations/install/). Administration and business permissions remain separate; the administrator must deliberately assign the sample's business permissions to test actors.

## Directory configuration

Kernel settings are under `Security:Ldap`, with `Enabled`, directory options, `ServiceUserName` and `ServicePassword`. Select the configured directory's provider ID in `Security:AuthenticationProvider` to use it for authentication. Keep service credentials in your deployment secret mechanism.

Configure host/port, transport, base DN, user/group filters and attribute mappings to match the real directory. Prefer LDAPS with a trusted CA and correct server name. A TLS failure does not trigger a plaintext fallback. The option contract is linked from the [configuration reference](../../reference/kernel-configuration/).

Qualification covers OpenLDAP on Linux with LDAPS; it does not establish Active Directory, StartTLS or nested-group support. Directory timeouts and paged search failures must remain visible rather than becoming permissive access.

Verify denied access, deactivated identities, revoked permissions and self-approval in addition to the happy path. See [authorization concepts](../../core-concepts/authorization/).

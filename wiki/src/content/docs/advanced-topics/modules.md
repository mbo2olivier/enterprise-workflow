---
title: "Module loading and isolation"
description: "Understand the trust and packaging boundaries."
sidebar:
  order: 3
---

The module catalogue resolves immutable artifact identities, validates manifests and content hashes, and composes registered handlers, processes and presentation resources. Exact owners remain available for durable instances and historical UI.

This is not a hostile-code sandbox. An installed module is trusted .NET code in the host process and can use the host's privileges. Review artifacts and dependencies before adding them. A content hash is not a publisher signature, notarization or an isolation boundary.

The CLI supports one public concrete fluent `WorkflowModule` entry point in the project's target assembly. Managed dependencies and declared `wwwroot` resources are manifested. Native/RID-specific module dependencies and satellite assemblies are rejected by the initial packaging format.

There is no general hot-unload promise. Use a controlled host restart for production catalogue updates and preserve exact owners. The development supervisor's generation switching is a local development mechanism, and those reserved artifacts are refused by production verification/hosting.

An API mismatch in retained code must be resolved using a compatible runtime/artifact set. The CLI reports `EW7050_INCOMPATIBLE_MODULE_API` for the handled member-access mismatch case. Starting a fresh local environment can help development; it is not a production data migration strategy.

See [deployment](../../operations/modules/) and [compatibility](../../reference/compatibility/).

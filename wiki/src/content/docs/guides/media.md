---
title: "Attachments and generated documents"
description: "Declare module policies and attach files through the handler context."
sidebar:
  order: 6
---

Declare formats, maximum size and visibility once in the module:

```csharp
builder.MediaPolicies(policies => policies.Add("documents")
    .AllowFormats(MediaFormats.Pdf, MediaFormats.Text)
    .MaximumSize(10.Megabytes())
    .VisibleToInstanceReaders());
```

Use the `EnterpriseWorkflow.Runtime` namespace for media contracts and helpers. A file field references the policy with `.File("documents", "Documents").UseMediaPolicy("documents").MaximumFiles(3)`. The field can narrow file count or require input; it cannot redefine the policy's formats or visibility.

## Collect files with the execution context

Inject `IWorkflowMediaService`, then use `var files = media.For(context)`. An initializer can call `await files.FromFieldAsync("documents", cancellationToken)` to collect uploaded references. A service can call `StoreAsync` with a stable operation key, file name, stream and policy ID. [LeaveRequest's closing service](../../examples/leave-request/) demonstrates generation.

Collected changes attach only with a successful engine commit. A rejected state update, stale lease or conflicting revision must not publish a file mutation. Do not pass a manual media list in success results; the context collection owns attachment.

The library uses persistent attachment and file visibility policy, independently of whether a media reference is displayed in the DTO. Hiding a DTO property does not hide the attached file; restrict its policy when needed.

## Storage and inspection

Set Kernel `Media:Root` to a durable private volume outside static files. Embedded hosts can call `AddEnterpriseWorkflowMedia(privateRoot)`. An `IWorkflowMediaStorageProvider` supplies another backend. Shared hosts need shared storage or a provider that supports the topology.

Built-in checks validate supported file signatures/structures; no antivirus is supplied by default. Custom scanners can reject uploads; optional inspection can leave files in durable quarantine until a trusted integration resolves their exact hash. Format and size limits are listed in [media reference](../../reference/media/).

Back up binaries together with database attachment metadata. Logical deletion does not guarantee immediate physical erasure; [backup and retention](../../operations/backup/) must cover both.

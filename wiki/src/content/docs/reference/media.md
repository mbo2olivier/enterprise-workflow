---
title: "Media contracts and limits"
description: "Supported formats, inspection and lifecycle boundaries."
sidebar:
  order: 6
---

`IWorkflowMediaService.For(context)` creates the scoped collection used by initializers, human completions and services. `FromFieldAsync` collects uploads; `StoreAsync` generates content; Get/Open/Replace/Delete operations use the execution's instance scope. Stable operation keys make retry behavior explicit.

Create/replace/delete effects become visible with the successful engine commit. Replacing produces an immutable revision; deleting logically removes access to all revisions. APIs that manually set attachment result lists are not the authoring path.

## Built-in formats

Supported families include PDF, JPEG/PNG/classic TIFF, UTF-8 TXT/CSV, RTF, DOCX/XLSX/PPTX and ODT/ODS/ODP. Legacy DOC/XLS/PPT, BigTIFF, generic archives, encrypted documents and macro-enabled variants are not built in. Office/ODF validation checks package structure and rejects unsafe structures; it is not a full office renderer or antivirus.

| Limit | Ceiling |
| --- | --- |
| File size | 100 MiB technical limit; policy may lower it |
| Files per field | 8; form may lower it |
| Attached references per commit | 32 |
| Office/ODF archive entries | 2,048 |
| Total decompressed size / one entry | 200 MiB / 100 MiB |
| XML size / depth | 32 MiB / 64 |

## Extension and maintenance

`IWorkflowMediaFormatValidator` can validate a module-owned format registered through `builder.MediaFormats`. The exact artifact's validator is required; absence, exception or false blocks the upload. Built-in IDs/extensions cannot be redefined.

`IWorkflowMediaScanner` is called before staging. Optional `IWorkflowMediaInspector` can leave content Pending in durable quarantine; a trusted integration completes inspection against the exact SHA-256. There is no generic public HTTP approval callback.

`PurgeExpiredStagingAsync` is an explicit operation with minimum age one hour and batches up to 1,000. Active/historical media are not automatically purged. Objects written before a failure preceding metadata may need storage reconciliation. See [media guide](../../guides/media/) and [backup](../../operations/backup/).

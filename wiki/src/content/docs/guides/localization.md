---
title: Localization
description: Installation languages, local user preferences and immutable business text catalogs.
---

These L11 APIs are under local qualification; they are not in the previously published release.

```json
{
  "Localization": {
    "DefaultCulture": "en",
    "FallbackCulture": "fr",
    "SupportedCultures": ["en", "fr"]
  }
}
```

The installation chooses its default and fallback languages. Existing hosts default to French with English fallback unless configured otherwise. Users switch language directly with the toggle next to search in the top bar; the reset button restores the installation default. The current page and query parameters are preserved. There is no dedicated profile page. Preferences are stored locally by identity, including external identities. Apply the new security database migrations before upgrading. Browser language headers do not override the installation or user preference.

## Author business text

Place catalogs in `Localization/en.json`, `Localization/fr.json`, etc., and embed them in the module project (the module template includes this convention):

```xml
<ItemGroup><EmbeddedResource Include="Localization/*.json" /></ItemGroup>
```

No catalog-provider class is required. The SDK loads conventional resources from the exact calling assembly and caches them by assembly instance, preserving isolation between module versions and load contexts. Declare stable metadata:

```csharp
using static EnterpriseWorkflow.Sdk.WorkflowTexts;

builder.AddForm("request", Text("form.title", "Leave request"), form =>
    form.HandledBy<ConfirmHandler>().Fields(fields =>
    {
        fields.Date("startDate", Text("fields.startDate", "Start date"));
        fields.Choice("type", Text("fields.type", "Leave type"))
            .Options(("paid", Text("choices.paid", "Paid leave")));
    }));
```

`WorkflowText` contains its key, fallback and a private immutable copy of the translations for that key. Metadata travels with the exact artifact rather than a mutable global registry. Form, process, stage and action builders accept localized texts. Literal labels remain supported. Missing translations use the configured fallback language, then the declared fallback text. Resolution occurs per display, never while compiling a definition. IDs, choice values and persisted business data do not change with the language.

For DTO labels, use `[LabelKey("fields.startDate", "Start date")]`. The compiler uses the embedded catalogs in the assembly declaring that property. The optional explicit `[LabelKey(typeof(MyCatalogProvider), key, fallback)]` form and `IWorkflowTextCatalogProvider` remain available for custom sources. The compiler stores text metadata, not the provider CLR type, in the state contract. Use a new module release/revision for changed declarations; retain immutable historical artifacts.

Import `WorkflowTexts` statically in module code and Razor components to call `Text(key, fallback)` directly. A shared helper library can use `WorkflowTexts.For<MyModule>().Text(key, fallback)` to choose the owning assembly explicitly. Modules without catalogs still use the declared fallback. Cached conventional catalogs are immutable; manual `new WorkflowTextCatalog().AddJson(...)` catalogs remain available.

Custom Razor components inject `IWorkflowTextLocalizer` from `EnterpriseWorkflow.Abstractions` and call `Resolve(text, fallback)`. Business validation can use `new WorkflowValidationError(code, localizedText, field)`. Background document generation must pass an explicit culture to the resolver. SMTP notifications resolve the recipient's supported preference or the installation default; custom senders can implement `ILocalizedWorkflowMailSender`.

See the bilingual `samples/LeaveRequest.Module` catalogs and custom summary component. Localization changes display labels, not authorization rules or workflow transitions.

using System.Text.RegularExpressions;

namespace EnterpriseWorkflow.Security;

[Flags]
public enum WorkflowBrandingFields { None = 0, DisplayName = 1, Logo = 2, AccentColor = 4, TimeZone = 8, Culture = 16, All = 31 }

/// <summary>Instance-wide host defaults and explicit administrative permissions.</summary>
public sealed class WorkflowBrandingOptions
{
    public PresentationSettings Defaults { get; set; } = PresentationSettings.Default;
    public WorkflowBrandingFields EditableFields { get; set; }
    public string? FaviconPath { get; set; }
    public string? StylesheetPath { get; set; }
    public string? LogoAccessibleLabel { get; set; }
    public string? AccentStrong { get; set; }
    public string? AccentSoft { get; set; }
    public string? Canvas { get; set; }
    public string? Surface { get; set; }
    public string? Ink { get; set; }
    public string? Muted { get; set; }
    public string? Border { get; set; }
    public bool CanEdit(WorkflowBrandingFields field) => (EditableFields & field) == field;
    public void Validate()
    {
        foreach (var color in new[] { Defaults.AccentColor, AccentStrong, AccentSoft, Canvas, Surface, Ink, Muted, Border })
            if (color is not null && !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)) throw new ArgumentException("Brand colors must be hexadecimal RGB values.");
        foreach (var path in new[] { Defaults.LogoResourcePath, FaviconPath, StylesheetPath })
            if (path is not null && (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') || path.Any(char.IsControl))) throw new ArgumentException("Brand assets must use local absolute URL paths.");
        if (string.IsNullOrWhiteSpace(Defaults.DisplayName) || Defaults.DisplayName.Length > 160) throw new ArgumentException("Invalid brand display name.");
    }
    public PresentationSettings Resolve(PresentationSettings stored) => new(
        CanEdit(WorkflowBrandingFields.DisplayName) ? stored.DisplayName : Defaults.DisplayName,
        CanEdit(WorkflowBrandingFields.Logo) ? stored.LogoResourcePath : Defaults.LogoResourcePath,
        CanEdit(WorkflowBrandingFields.AccentColor) ? stored.AccentColor : Defaults.AccentColor,
        CanEdit(WorkflowBrandingFields.TimeZone) ? stored.TimeZoneId : Defaults.TimeZoneId,
        CanEdit(WorkflowBrandingFields.Culture) ? stored.Culture : Defaults.Culture,
        stored.Revision);
    public string CssVariables(string accent) => $"--accent:{accent};--accent-strong:{AccentStrong ?? "color-mix(in srgb,var(--accent),black 18%)"};--accent-soft:{AccentSoft ?? "color-mix(in srgb,var(--accent),white 90%)"}" +
        (Canvas is null ? "" : $";--canvas:{Canvas}") + (Surface is null ? "" : $";--surface:{Surface}") +
        (Ink is null ? "" : $";--ink:{Ink}") + (Muted is null ? "" : $";--muted:{Muted}") + (Border is null ? "" : $";--line:{Border}");
}

/// <summary>Enforces policy on both effective reads and direct writes, preserving revision checks in the backing store.</summary>
public sealed class WorkflowBrandedSettingsStore(IPresentationSettingsStore inner, WorkflowBrandingOptions options) : IPresentationSettingsStore
{
    public async ValueTask<ProviderResult<PresentationSettings>> ReadAsync(CancellationToken cancellationToken)
    {
        var result = await inner.ReadAsync(cancellationToken).ConfigureAwait(false);
        return result.Value is { } value ? result with { Value = value.Revision == 0 ? options.Defaults with { Revision = 0 } : options.Resolve(value) } : result;
    }
    public async ValueTask<ProviderResult<PresentationSettings>> UpdateAsync(PresentationSettings settings, IdentityReference actor, CancellationToken cancellationToken)
    {
        if (settings != options.Resolve(settings)) return new(ProviderOutcome.Forbidden, ErrorCode: "branding.locked");
        return await inner.UpdateAsync(settings, actor, cancellationToken).ConfigureAwait(false);
    }
}

using EnterpriseWorkflow.Security;
using Xunit;

namespace EnterpriseWorkflow.Security.Tests;

public sealed class WorkflowBrandingTests
{
    [Fact]
    public async Task HostOverridesLockedPersistedValuesAndDirectMutationsAreForbidden()
    {
        var backing = new SettingsStore(PresentationSettings.Default with { DisplayName = "Old persisted name", Revision = 5 });
        var options = new WorkflowBrandingOptions { Defaults = PresentationSettings.Default with { DisplayName = "Bank", AccentColor = "#123456" }, EditableFields = WorkflowBrandingFields.TimeZone };
        var store = new WorkflowBrandedSettingsStore(backing, options);
        var effective = (await store.ReadAsync(TestContext.Current.CancellationToken)).Value!;
        Assert.Equal("Bank", effective.DisplayName); Assert.Equal("#123456", effective.AccentColor); Assert.Equal(5, effective.Revision);
        var actor = new IdentityReference("local", "admin");
        var denied = await store.UpdateAsync(effective with { DisplayName = "Changed" }, actor, TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Forbidden, denied.Outcome); Assert.Equal(0, backing.Writes);
        var accepted = await store.UpdateAsync(effective with { TimeZoneId = "Africa/Kinshasa" }, actor, TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Succeeded, accepted.Outcome); Assert.Equal(1, backing.Writes);
    }
    [Fact]
    public void BrandTokensRejectCssInjectionAndRemoteAssets()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowBrandingOptions { Canvas = "red;display:none" }.Validate());
        Assert.Throws<ArgumentException>(() => new WorkflowBrandingOptions { FaviconPath = "//third-party/logo" }.Validate());
        var options = new WorkflowBrandingOptions { Canvas = "#123456" }; options.Validate();
        Assert.Contains("--canvas:#123456", options.CssVariables("#1768E5"), StringComparison.Ordinal);
    }
    private sealed class SettingsStore(PresentationSettings value) : IPresentationSettingsStore
    {
        public int Writes { get; private set; }
        public ValueTask<ProviderResult<PresentationSettings>> ReadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(new ProviderResult<PresentationSettings>(ProviderOutcome.Succeeded, value));
        public ValueTask<ProviderResult<PresentationSettings>> UpdateAsync(PresentationSettings settings, IdentityReference actor, CancellationToken cancellationToken)
        { Writes++; return ValueTask.FromResult(new ProviderResult<PresentationSettings>(ProviderOutcome.Succeeded, settings)); }
    }
}

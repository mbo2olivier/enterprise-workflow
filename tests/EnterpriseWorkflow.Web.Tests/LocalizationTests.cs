using System.Globalization;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Sdk;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void ConfiguredLanguagesReplaceDefaultsWithoutHidingRealDuplicates()
    {
        var configuration = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(
            """{"Localization":{"DefaultCulture":"fr","FallbackCulture":"en","SupportedCultures":["fr","en"]}}"""))).Build();
        var options = WorkflowLocalizationOptions.FromConfiguration(configuration);
        Assert.Equal(2, options.SupportedCultures.Length);
        Assert.Equal("fr", options.SupportedCultures[0]);
        Assert.Equal("en", options.SupportedCultures[1]);
        configuration["Localization:SupportedCultures:1"] = "FR";
        Assert.Throws<InvalidOperationException>(() => WorkflowLocalizationOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void MissingLanguagesKeepDefaultsButExplicitEmptyArraysAreInvalid()
    {
        var defaults = WorkflowLocalizationOptions.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.Equal(2, defaults.SupportedCultures.Length);
        Assert.Equal("en", defaults.SupportedCultures[0]);
        var empty = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(
            """{"Localization":{"SupportedCultures":[]}}"""))).Build();
        Assert.Throws<InvalidOperationException>(() => WorkflowLocalizationOptions.FromConfiguration(empty));
        var single = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(
            """{"Localization":{"DefaultCulture":"fr","FallbackCulture":"fr","SupportedCultures":["fr"]}}"""))).Build();
        Assert.Equal("fr", Assert.Single(WorkflowLocalizationOptions.FromConfiguration(single).SupportedCultures));
    }

    [Fact]
    public void CatalogsAndFormProjectionsAreIndependentOfCompilationCultureAndKeepValuesStable()
    {
        using var french = new MemoryStream(Encoding.UTF8.GetBytes("""{"fields.date":"Date de début","choices.paid":"Congé payé"}"""));
        var catalog = new WorkflowTextCatalog().AddJson("fr", french);
        var label = catalog.Text("fields.date", "Start date");
        var choice = catalog.Text("choices.paid", "Paid leave");
        var form = new WorkflowFormDefinition(new(new("leave.form"), 1), "Leave request",
            [new(new("type"), choice.Fallback, WorkflowFormFieldKind.Choice, true, WorkflowFieldAudience.Initiator,
                Options: [new("paid", choice.Fallback) { LocalizedLabel = choice }]) { LocalizedLabel = label }]);
        var originalJson = JsonSerializer.Serialize(form);
        var localizer = new WorkflowTextLocalizer(new() { FallbackCulture = "en" });
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            var fr = localizer.ResolveForm(form);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var en = localizer.ResolveForm(form);
            Assert.Equal("Date de début", fr.Fields[0].Label);
            Assert.Equal("Start date", en.Fields[0].Label);
            Assert.Equal("paid", fr.Fields[0].Options[0].Value);
            Assert.Equal("paid", en.Fields[0].Options[0].Value);
            Assert.Equal("Congé payé", fr.Fields[0].Options[0].Label);
            Assert.Equal(originalJson, JsonSerializer.Serialize(form));
            var roundTrip = JsonSerializer.Deserialize<WorkflowFormDefinition>(originalJson)!;
            Assert.Equal("Date de début", roundTrip.Fields[0].LocalizedLabel!.Resolve("fr-FR", "en"));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void FallbackLanguageIsConfigurableAndInstanceCulturesAreValidated()
    {
        var text = new WorkflowText("greeting", "Hello", new Dictionary<string, string> { ["fr"] = "Bonjour" });
        Assert.Equal("Bonjour", text.Resolve("de", "fr"));
        Assert.Equal("Hello", text.Resolve("de", "en"));
        Assert.Throws<InvalidOperationException>(() => new WorkflowLocalizationOptions { DefaultCulture = "de" }.Validate());
        var localizer = new WorkflowUiLocalizer(new());
        Assert.Equal("Sign in", localizer.Resolve("Se connecter", "en"));
        Assert.Equal("Se connecter", localizer.Resolve("Se connecter", "fr"));
    }

    [Fact]
    public void StateLabelMetadataCanBeAuthoredWithoutTranslatingTheStateSchemaValues()
    {
        var contract = WorkflowStateContractCompiler.Compile<LocalizedState>(1);
        var field = Assert.Single(contract.Root.Fields);
        Assert.Equal("Date de début", field.LocalizedLabel!.Resolve("fr", "en"));
        Assert.Equal("startDate", field.Name);
        Assert.Equal("date", field.Kind);
    }

    public sealed class CatalogProvider : IWorkflowTextCatalogProvider
    {
        public WorkflowTextCatalog Catalog
        {
            get
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"date":"Date de début"}"""));
                return new WorkflowTextCatalog().AddJson("fr", stream);
            }
        }
    }
    public sealed class LocalizedState
    {
        [System.Text.Json.Serialization.JsonPropertyName("startDate"), LabelKey(typeof(CatalogProvider), "date", "Start date")]
        public DateOnly StartDate { get; init; }
    }
}

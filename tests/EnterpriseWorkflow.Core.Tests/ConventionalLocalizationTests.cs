using System.Globalization;
using System.Runtime.Loader;
using System.Text;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Sdk;
using Xunit;
using static EnterpriseWorkflow.Sdk.WorkflowTexts;

namespace EnterpriseWorkflow.Core.Tests;

public sealed class ConventionalLocalizationTests
{
    [Fact]
    public void DirectTextHelperFindsTheCallersEmbeddedCatalogs()
    {
        var text = Text("l11.convention.greeting", "Hello");
        Assert.Equal("Bonjour", text.Resolve("fr", "en"));
        Assert.Equal("Hello", text.Resolve("en", "fr"));
        Assert.Equal("Bonjour", text.Resolve("de", "fr"));
        var missing = Text("l11.convention.missing", "Declared fallback");
        Assert.Equal("Declared fallback", missing.Resolve("fr", "en"));
    }

    [Fact]
    public void LabelsNeedNoProviderAndKeepCompilationIndependentOfUserCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            var french = WorkflowStateContractCompiler.Compile<State>(1);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var english = WorkflowStateContractCompiler.Compile<State>(1);
            var field = Assert.Single(french.Root.Fields);
            Assert.Equal("Date de début", field.LocalizedLabel!.Resolve("fr", "en"));
            Assert.Equal("Start date", field.Label);
            Assert.Equal("startDate", field.Name);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(french), System.Text.Json.JsonSerializer.Serialize(english));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void CachedCatalogIsImmutableAndScopedToTheExactAssemblyLoadContext()
    {
        var original = WorkflowTexts.For<ConventionalLocalizationTests>();
        Assert.Same(original, WorkflowTexts.For<ConventionalLocalizationTests>());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        Assert.Throws<InvalidOperationException>(() => original.AddJson("de", stream));
        var context = new AssemblyLoadContext("L11-localization-test", isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(ConventionalLocalizationTests).Assembly.Location);
            var isolated = WorkflowTextCatalog.FromAssembly(assembly);
            Assert.NotSame(original, isolated);
            Assert.Equal("Bonjour", isolated.Text("l11.convention.greeting", "Hello").Resolve("fr", "en"));
        }
        finally { context.Unload(); }
    }

    [Fact]
    public void AssembliesWithoutCatalogsCanUseDeclaredFallbacks()
    {
        var text = WorkflowTexts.For<WorkflowText>().Text("absent", "Literal fallback");
        Assert.Empty(text.Translations);
        Assert.Equal("Literal fallback", text.Resolve("fr", "en"));
    }

    public sealed class State
    {
        [System.Text.Json.Serialization.JsonPropertyName("startDate"), LabelKey("l11.convention.date", "Start date")]
        public DateOnly StartDate { get; init; }
    }
}

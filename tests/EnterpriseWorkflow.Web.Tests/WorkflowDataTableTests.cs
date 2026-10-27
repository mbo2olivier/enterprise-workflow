using System.Text.Json;
using EnterpriseWorkflow.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class WorkflowDataTableTests
{
    [Fact]
    public async Task NestedCommentsDisplayAccentsAndRemainHtmlEncoded()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        using var document = JsonDocument.Parse("""{"comments":[{"text":"Refus motiv\u00E9 <script>alert(1)</script>"}]}""");
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<WorkflowDataTable>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Data"] = document.RootElement }));
            return component.ToHtmlString();
        });
        var text = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("Refus motivé", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u00E9", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }
}

using System.Globalization;
using System.Net;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class LocalizedNavigationTests
{
    [Theory]
    [InlineData("en", "History", "Comments", "Library")]
    [InlineData("fr", "Historique", "Commentaires", "Bibliothèque")]
    public async Task DetailTabsTranslateWithoutChangingTheirLinks(string culture, string history, string comments, string library)
    {
        var id = Guid.NewGuid();
        var html = await RenderAsync<WorkflowInstanceTabs>(culture, new() { ["InstanceId"] = id, ["ActiveTab"] = "history" });
        var text = WebUtility.HtmlDecode(html);
        Assert.Contains(">" + history + "</a>", text, StringComparison.Ordinal);
        Assert.Contains(">" + comments + "</a>", text, StringComparison.Ordinal);
        Assert.Contains(">" + library + "</a>", text, StringComparison.Ordinal);
        Assert.Contains($"/instances/{id}?tab=history", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", WorkflowInstanceStatus.Waiting, null, "Waiting")]
    [InlineData("fr", WorkflowInstanceStatus.Waiting, null, "En attente")]
    [InlineData("en", WorkflowInstanceStatus.Completed, WorkflowCompletionOutcome.Declined, "Declined")]
    [InlineData("fr", WorkflowInstanceStatus.Completed, WorkflowCompletionOutcome.Declined, "Refusée")]
    [InlineData("en", WorkflowInstanceStatus.Completed, WorkflowCompletionOutcome.Cancelled, "Cancelled")]
    [InlineData("en", WorkflowInstanceStatus.Completed, WorkflowCompletionOutcome.Succeeded, "Completed")]
    [InlineData("en", WorkflowInstanceStatus.Failed, null, "Failed")]
    public async Task ListRowsTranslateStatusesIncludingCompletionOutcomes(string culture, WorkflowInstanceStatus status, WorkflowCompletionOutcome? outcome, string label)
    {
        var request = new WorkflowRequestView(new(Guid.NewGuid()), "Leave request", 10, status, 1, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, CompletionOutcome: outcome);
        var html = await RenderAsync<WorkflowRequestRow>(culture, new() { ["Request"] = request, ["Href"] = "/instances/test" });
        Assert.Contains(">" + label + "</span>", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/instances/123?tab=comments", "/instances/123?tab=comments")]
    [InlineData("/search?status=Waiting&process=leave", "/search?status=Waiting&process=leave")]
    [InlineData("//external.example/", "/")]
    [InlineData("https://external.example/", "/")]
    [InlineData("/\\external.example/", "/")]
    public void LanguageSwitchReturnStaysLocalAndPreservesQuery(string value, string expected) =>
        Assert.Equal(expected, EnterpriseWorkflowWebExtensions.SafeReturnUrl(value));

    private static async Task<string> RenderAsync<T>(string culture, Dictionary<string, object?> parameters) where T : IComponent
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var services = new ServiceCollection().AddLogging();
            services.AddEnterpriseWorkflowLocalization();
            await using var provider = services.BuildServiceProvider();
            await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
            return await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}

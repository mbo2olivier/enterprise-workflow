using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EnterpriseWorkflow.Web.Tests;

public sealed class WorkflowValidationTests
{
    [Fact]
    public void FeedbackIsBoundToReaderAndPageSingleUseAndExcludesAntiforgerySecrets()
    {
        var store = new WorkflowFormFeedbackStore();
        var actor = new IdentityReference("local", "alice");
        var form = new FormCollection(new Dictionary<string, StringValues> { ["field.reason"] = "My reason", ["__RequestVerificationToken"] = "secret", ["idempotencyKey"] = "same-key" });
        var key = store.Save(actor, "/tasks/one", [new("reason", "required")], null, form);
        Assert.Null(store.Take(key, new("local", "bob"), "/tasks/one"));
        Assert.Null(store.Take(key, actor, "/tasks/two"));
        var feedback = store.Take(key, actor, "/tasks/one");
        Assert.NotNull(feedback);
        Assert.Equal("My reason", feedback.Values["field.reason"]);
        Assert.Equal("same-key", feedback.Values["idempotencyKey"]);
        Assert.DoesNotContain("__RequestVerificationToken", feedback.Values.Keys);
        Assert.Null(store.Take(key, actor, "/tasks/one"));
    }

    [Fact]
    public async Task MultipleErrorsAreRenderedAtTheirFieldsWithEscapedMessagesAndPreservedValues()
    {
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var form = new WorkflowFormDefinition(new(new("form"), 1), "Formulaire", [
            new(new("reason"), "Motif", WorkflowFormFieldKind.ShortText, true, WorkflowFieldAudience.Initiator),
            new(new("date"), "Date", WorkflowFormFieldKind.Date, true, WorkflowFieldAudience.Initiator)]);
        var errors = new WorkflowFormError[] { new("reason", "custom", "Motif <script>invalide</script>"), new("date", "presentation.form-field-date-expected") };
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<WorkflowFormFields>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            ["Form"] = form, ["Errors"] = errors, ["SubmittedValues"] = new Dictionary<string, string> { ["field.reason"] = "Texte conservé" }
        }))).ToHtmlString());
        Assert.Contains("aria-invalid=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"field.reason-error\"", html, StringComparison.Ordinal);
        Assert.Contains("field.date-error", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("Texte conservé", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Contains("Saisissez une date valide.", html, StringComparison.Ordinal);
        var summary = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<WorkflowValidationSummary>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Form"] = form, ["Errors"] = errors }))).ToHtmlString());
        Assert.Contains("href=\"#field.reason\"", summary, StringComparison.Ordinal);
        Assert.Contains("href=\"#field.date\"", summary, StringComparison.Ordinal);
    }
}

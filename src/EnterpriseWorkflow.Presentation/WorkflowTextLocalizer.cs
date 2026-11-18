using System.Collections.Immutable;
using System.Globalization;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Presentation;

public sealed class WorkflowTextLocalizationOptions
{
    public string FallbackCulture { get; set; } = "en";
}

/// <summary>Stateless resolver; request culture is read on every call, never captured in a singleton.</summary>
public sealed class WorkflowTextLocalizer(WorkflowTextLocalizationOptions? options = null) : IWorkflowTextLocalizer
{
    public string Resolve(WorkflowText? text, string fallback, string? culture = null) =>
        text?.Resolve(culture ?? CultureInfo.CurrentUICulture.Name, options?.FallbackCulture ?? "en") ?? fallback;

    public WorkflowFormDefinition ResolveForm(WorkflowFormDefinition form) => form with
    {
        Title = Resolve(form.LocalizedTitle, form.Title),
        Fields = form.Fields.Select(field => field with
        {
            Label = Resolve(field.LocalizedLabel, field.Label),
            HelpText = field.HelpText is null ? null : Resolve(field.LocalizedHelp, field.HelpText),
            Options = field.Options.IsDefault ? field.Options : field.Options.Select(choice => choice with
            { Label = Resolve(choice.LocalizedLabel, choice.Label) }).ToImmutableArray(),
        }).ToImmutableArray(),
    };

    public WorkflowStagePolicy? ResolvePolicy(WorkflowStagePolicy? policy) => policy is null ? null : policy with
    {
        Actions = policy.Actions.ToDictionary(pair => pair.Key, pair => pair.Value with
        { Label = Resolve(pair.Value.LocalizedLabel, pair.Value.Label) }, StringComparer.Ordinal),
    };
}

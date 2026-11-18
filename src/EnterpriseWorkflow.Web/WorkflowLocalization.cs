using System.Globalization;
using System.Text.Json;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EnterpriseWorkflow.Web;

public sealed class WorkflowLocalizationOptions
{
    public string DefaultCulture { get; set; } = "fr";
    public string FallbackCulture { get; set; } = "en";
    public string[] SupportedCultures { get; set; } = ["en", "fr"];
    public void Validate()
    {
        if (SupportedCultures is null || SupportedCultures.Length is 0 or > 32 || SupportedCultures.Distinct(StringComparer.OrdinalIgnoreCase).Count() != SupportedCultures.Length)
            throw new InvalidOperationException("Localization cultures must be non-empty and unique.");
        foreach (var name in SupportedCultures)
            if (string.IsNullOrWhiteSpace(name) || name.Length > 32 || CultureInfo.GetCultureInfo(name).Name.Length == 0)
                throw new InvalidOperationException("Invalid localization culture.");
        if (!SupportedCultures.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase) || !SupportedCultures.Contains(FallbackCulture, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Default and fallback cultures must be supported cultures.");
    }
}

public sealed class WorkflowUiLocalizer(WorkflowLocalizationOptions options)
{
    public string FallbackCulture => options.FallbackCulture;
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = Load();
    public string this[string key] => Resolve(key, CultureInfo.CurrentUICulture.Name);
    public string Resolve(string key, string culture)
    {
        foreach (var candidate in new[] { culture, CultureInfo.GetCultureInfo(culture).TwoLetterISOLanguageName,
            options.FallbackCulture, CultureInfo.GetCultureInfo(options.FallbackCulture).TwoLetterISOLanguageName })
            if (Catalogs.TryGetValue(candidate, out var catalog) && catalog.TryGetValue(key, out var value)) return value;
        return key;
    }
    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        var assembly = typeof(WorkflowUiLocalizer).Assembly;
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in new[] { "en", "fr" })
        {
            using var stream = assembly.GetManifestResourceStream($"EnterpriseWorkflow.Web.Localization.{culture}.json")!;
            result[culture] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }
        return result;
    }
}

public static class WorkflowLocalizationExtensions
{
    public static IServiceCollection AddEnterpriseWorkflowLocalization(this IServiceCollection services, WorkflowLocalizationOptions? options = null)
    {
        options ??= new();
        options.Validate();
        services.TryAddSingleton(options);
        services.AddSingleton(new WorkflowTextLocalizationOptions { FallbackCulture = options.FallbackCulture });
        services.TryAddSingleton<WorkflowTextLocalizer>();
        services.TryAddSingleton<WorkflowUiLocalizer>();
        return services;
    }

    public static IApplicationBuilder UseEnterpriseWorkflowLocalization(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<WorkflowLocalizationOptions>();
        options.Validate();
        var cultures = options.SupportedCultures.Select(CultureInfo.GetCultureInfo).ToArray();
        return app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(options.DefaultCulture),
            SupportedCultures = cultures, SupportedUICultures = cultures,
            // Installation default is deliberate: browser headers do not override it.
            RequestCultureProviders = [new CustomRequestCultureProvider(async context =>
            {
                var provider = context.User.FindFirst(WorkflowSessionAuthenticationHandler.ProviderClaim)?.Value;
                var subject = context.User.FindFirst(WorkflowSessionAuthenticationHandler.SubjectClaim)?.Value;
                if (provider is null || subject is null || context.RequestServices.GetService<IUserCulturePreferenceStore>() is not { } store) return null;
                var culture = await store.ReadCultureAsync(new(provider, subject), context.RequestAborted).ConfigureAwait(false);
                return culture is not null && options.SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase)
                    ? new ProviderCultureResult(culture) : null;
            })],
        });
    }
}

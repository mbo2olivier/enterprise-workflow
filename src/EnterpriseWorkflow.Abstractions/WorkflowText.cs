using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace EnterpriseWorkflow.Abstractions;

public interface IWorkflowTextLocalizer
{
    string Resolve(WorkflowText? text, string fallback, string? culture = null);
}


/// <summary>Immutable presentation metadata. Never resolve this while compiling a definition.</summary>
public sealed record WorkflowText
{
    public string Key { get; }
    public string Fallback { get; }
    public IReadOnlyDictionary<string, string> Translations { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public WorkflowText(string key, string fallback, IReadOnlyDictionary<string, string>? translations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        if (key.Length > 160 || fallback.Length > 2000 || translations?.Count > 32)
            throw new ArgumentException("Localized text exceeds its bounds.");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in translations ?? new Dictionary<string, string>())
        {
            var culture = CultureInfo.GetCultureInfo(pair.Key).Name;
            if (culture.Length == 0 || string.IsNullOrEmpty(pair.Value) || pair.Value.Length > 2000 || !values.TryAdd(culture, pair.Value))
                throw new ArgumentException("Invalid localized text translation.", nameof(translations));
        }
        Key = key; Fallback = fallback; Translations = new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));
    }

    public string Resolve(string culture, string fallbackCulture)
    {
        if (TryResolve(culture, out var translated) || TryResolve(fallbackCulture, out translated)) return translated;
        return Fallback;
    }

    private bool TryResolve(string culture, out string translated)
    {
        for (var candidate = CultureInfo.GetCultureInfo(culture); candidate.Name.Length > 0; candidate = candidate.Parent)
            if (Translations.TryGetValue(candidate.Name, out translated!)) return true;
        translated = Fallback;
        return false;
    }
}

/// <summary>Module-local catalogs materialized into immutable metadata in the exact module artifact.</summary>
public sealed class WorkflowTextCatalog
{
    private static readonly ConditionalWeakTable<Assembly, Lazy<WorkflowTextCatalog>> AssemblyCatalogs = new();
    private bool _readOnly;
    private readonly Dictionary<string, Dictionary<string, string>> _cultures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads embedded Localization/&lt;culture&gt;.json resources from this exact assembly, including its load context.</summary>
    public static WorkflowTextCatalog FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return AssemblyCatalogs.GetValue(assembly, owner => new Lazy<WorkflowTextCatalog>(() => LoadAssembly(owner))).Value;
    }

    private static WorkflowTextCatalog LoadAssembly(Assembly assembly)
    {
        var catalog = new WorkflowTextCatalog();
        foreach (var name in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            const string marker = "Localization.";
            var index = name.LastIndexOf("." + marker, StringComparison.Ordinal);
            var start = index >= 0 ? index + marker.Length + 1 : name.StartsWith(marker, StringComparison.Ordinal) ? marker.Length : -1;
            if (start < 0 || !name.EndsWith(".json", StringComparison.Ordinal)) continue;
            var culture = name[start..^5];
            if (culture.Contains('.')) continue;
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException("An embedded localization catalog could not be opened.");
            catalog.AddJson(culture, stream);
        }
        catalog._readOnly = true;
        return catalog;
    }

    public WorkflowTextCatalog AddJson(string culture, Stream utf8Json)
    {
        if (_readOnly) throw new InvalidOperationException("Assembly localization catalogs are immutable. Create a separate catalog for custom resources.");
        ArgumentNullException.ThrowIfNull(utf8Json);
        var name = CultureInfo.GetCultureInfo(culture).Name;
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(utf8Json) ?? throw new JsonException("A text catalog must be an object.");
        if (name.Length == 0 || entries.Count > 4096 || entries.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 160 || string.IsNullOrEmpty(pair.Value) || pair.Value.Length > 2000))
            throw new ArgumentException("Invalid text catalog.");
        if (!_cultures.TryAdd(name, entries)) throw new ArgumentException("Duplicate catalog culture.", nameof(culture));
        return this;
    }

    public WorkflowText Text(string key, string fallback) => new(key, fallback,
        _cultures.Where(pair => pair.Value.ContainsKey(key)).ToDictionary(pair => pair.Key, pair => pair.Value[key], StringComparer.OrdinalIgnoreCase));
}

using System.Collections;
using Microsoft.Extensions.Configuration;

/// <summary>Reads data, never shell commands. Values are private to the development host.</summary>
internal static class DevelopmentEnvironment
{
    internal const string HostPrefix = "EW_DEV_HOST_";

    internal static async Task<Dictionary<string, string?>> ReadAsync(string root, string? file, bool disabled)
    {
        if (disabled && file is not null) throw new ArgumentException("--env-file and --no-env-file cannot be combined.");
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!disabled)
        {
            var path = file is null ? Path.Combine(root, ".env") : Path.GetFullPath(file);
            if (file is not null && !File.Exists(path)) throw new ArgumentException("The explicitly selected environment file does not exist.");
            if (File.Exists(path))
            {
                var lineNumber = 0;
                foreach (var line in await File.ReadAllLinesAsync(path))
                {
                    lineNumber++;
                    var text = line.Trim();
                    if (text.Length == 0 || text.StartsWith('#')) continue;
                    var equals = text.IndexOf('=');
                    if (equals <= 0) throw InvalidLine(lineNumber);
                    var key = text[..equals].Trim();
                    if (!char.IsAsciiLetter(key[0]) && key[0] != '_' || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
                        throw InvalidLine(lineNumber);
                    var value = text[(equals + 1)..].Trim();
                    if (value.StartsWith('\'') || value.StartsWith('"'))
                    {
                        if (value.Length < 2 || value[^1] != value[0]) throw InvalidLine(lineNumber);
                        value = value[1..^1];
                    }
                    if (!values.TryAdd(key, value)) throw InvalidLine(lineNumber);
                }
            }
        }
        // Do not mutate the CLI environment: builds must not inherit file secrets.
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            values[(string)entry.Key] = (string?)entry.Value;
        return values;
    }

    internal static IConfigurationRoot Configuration(IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(pair =>
            new KeyValuePair<string, string?>(pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value))).Build();

    internal static Dictionary<string, string?> HostValues(IReadOnlyDictionary<string, string?> values) =>
        values.Where(pair => !pair.Key.StartsWith("Security__", StringComparison.OrdinalIgnoreCase) &&
                            !pair.Key.StartsWith("Development__", StringComparison.OrdinalIgnoreCase) &&
                            !pair.Key.StartsWith(HostPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    internal static Dictionary<string, string?> ReadHostValues() => Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>().Where(entry => ((string)entry.Key).StartsWith(HostPrefix, StringComparison.Ordinal))
        .ToDictionary(entry => ((string)entry.Key)[HostPrefix.Length..], entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);

    private static ArgumentException InvalidLine(int number) => new($"Invalid environment file entry at line {number}. Values are not displayed.");
}

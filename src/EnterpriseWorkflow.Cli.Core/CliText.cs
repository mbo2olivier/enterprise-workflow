using System.Globalization;

/// <summary>Console presentation only; machine-readable output always stays invariant.</summary>
internal static class CliText
{
    internal static string Get(string french, string english) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? french : english;
    internal static string Format(string french, string english, params object?[] values) =>
        string.Format(CultureInfo.CurrentCulture, Get(french, english), values);
}

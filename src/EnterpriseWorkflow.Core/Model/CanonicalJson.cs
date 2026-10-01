using System.Text;
using System.Text.Json;

namespace EnterpriseWorkflow.Core.Model;

/// <summary>
/// An immutable JSON value with deterministic object-property ordering.
/// </summary>
public sealed class CanonicalJson
{
    private CanonicalJson(string originalText, string canonicalText, int originalByteCount, int canonicalByteCount)
    {
        OriginalText = originalText;
        CanonicalText = canonicalText;
        OriginalByteCount = originalByteCount;
        CanonicalByteCount = canonicalByteCount;
    }

    /// <summary>Gets the received JSON text.</summary>
    public string OriginalText { get; }

    /// <summary>Gets deterministic JSON with ordinal object-property ordering.</summary>
    public string CanonicalText { get; }

    /// <summary>Gets the UTF-8 size of the received representation.</summary>
    public int OriginalByteCount { get; }

    /// <summary>Gets the UTF-8 size of the canonical representation.</summary>
    public int CanonicalByteCount { get; }

    /// <summary>
    /// Parses and normalizes a JSON object or throws when the durable JSON contract is violated.
    /// </summary>
    public static CanonicalJson CreateObject(string json, int maximumBytes, int maximumDepth = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);

        if (!TryCreate(json, maximumBytes, maximumDepth, out var value, out var errorCode, out var errorMessage))
        {
            throw new FormatException($"{errorCode}: {errorMessage}");
        }

        return value!;
    }

    /// <summary>Returns a detached JSON element for the canonical value.</summary>
    public JsonElement ToJsonElement()
    {
        using var document = JsonDocument.Parse(CanonicalText);
        return document.RootElement.Clone();
    }

    internal static bool TryCreate(
        string? json,
        int maximumBytes,
        int maximumDepth,
        out CanonicalJson? value,
        out string errorCode,
        out string errorMessage)
    {
        value = null;
        errorCode = string.Empty;
        errorMessage = string.Empty;

        if (json is null)
        {
            errorCode = "EW1201";
            errorMessage = "JSON is required.";
            return false;
        }

        var originalByteCount = Encoding.UTF8.GetByteCount(json);
        if (originalByteCount > maximumBytes)
        {
            errorCode = "EW1202";
            errorMessage = $"JSON uses {originalByteCount} UTF-8 bytes; the limit is {maximumBytes}.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = maximumDepth,
            });
        }
        catch (JsonException exception)
        {
            errorCode = "EW1203";
            errorMessage = $"JSON is invalid: {exception.Message}";
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                errorCode = "EW1204";
                errorMessage = "The JSON root must be an object.";
                return false;
            }

            if (TryFindDuplicateProperty(document.RootElement, "$", out var duplicateLocation))
            {
                errorCode = "EW1205";
                errorMessage = $"JSON contains a duplicate object property at {duplicateLocation}.";
                return false;
            }

            var canonicalBytes = SerializeElement(document.RootElement);
            if (canonicalBytes.Length > maximumBytes)
            {
                errorCode = "EW1202";
                errorMessage = $"Canonical JSON uses {canonicalBytes.Length} UTF-8 bytes; the limit is {maximumBytes}.";
                return false;
            }

            value = new CanonicalJson(json, Encoding.UTF8.GetString(canonicalBytes), originalByteCount, canonicalBytes.Length);
            return true;
        }
    }

    private static byte[] SerializeElement(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, element);
        }

        return stream.ToArray();
    }

    internal static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}.");
        }
    }

    private static bool TryFindDuplicateProperty(JsonElement element, string path, out string duplicateLocation)
    {
        if (element.ValueKind is JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    duplicateLocation = propertyPath;
                    return true;
                }

                if (TryFindDuplicateProperty(property.Value, propertyPath, out duplicateLocation))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind is JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindDuplicateProperty(item, $"{path}[{index}]", out duplicateLocation))
                {
                    return true;
                }

                index++;
            }
        }

        duplicateLocation = string.Empty;
        return false;
    }
}

namespace EnterpriseWorkflow.Core.Model;

public sealed record WorkflowMediaFormatDefinition(string Id, string ContentType, string[] Extensions, bool BuiltIn = false)
{
    public void Validate()
    {
        if (!EnterpriseWorkflow.Abstractions.TechnicalId.IsValid(Id) || string.IsNullOrWhiteSpace(ContentType) || ContentType.Length > 200 ||
            !ContentType.Contains('/') || ContentType.Any(char.IsWhiteSpace) || Extensions is not { Length: > 0 and <= 16 } ||
            Extensions.Any(extension => string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.') || extension.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '.')))
            throw new FormatException("Invalid media format declaration.");
    }
}

/// <summary>Framework-owned format definitions. Validators are provided by the runtime.</summary>
public static class WorkflowMediaFormats
{
    public static IReadOnlyList<WorkflowMediaFormatDefinition> BuiltIns { get; } = Array.AsReadOnly(new[]
    {
        Format("pdf", "application/pdf", ".pdf"), Format("jpeg", "image/jpeg", ".jpg", ".jpeg"), Format("png", "image/png", ".png"),
        Format("text", "text/plain", ".txt"), Format("csv", "text/csv", ".csv"), Format("rtf", "application/rtf", ".rtf"), Format("tiff", "image/tiff", ".tif", ".tiff"),
        Format("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx"),
        Format("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"),
        Format("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx"),
        Format("odt", "application/vnd.oasis.opendocument.text", ".odt"), Format("ods", "application/vnd.oasis.opendocument.spreadsheet", ".ods"),
        Format("odp", "application/vnd.oasis.opendocument.presentation", ".odp")
    });
    private static WorkflowMediaFormatDefinition Format(string id, string mime, params string[] extensions) => new("framework." + id, mime, extensions, true);
    public static WorkflowMediaFormatDefinition? Find(string id) => BuiltIns.SingleOrDefault(format => format.Id == id || format.Id == "framework." + id);
}

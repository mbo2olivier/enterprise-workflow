using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Presentation;

public sealed record WorkflowFormError(string FieldId, string Code);

public sealed record WorkflowFormValidationResult(
    bool Succeeded,
    CanonicalJson? Submission,
    ImmutableArray<WorkflowFormError> Errors);

public interface IWorkflowFormService
{
    WorkflowFormValidationResult Validate(WorkflowFormDefinition form, string json);
    CanonicalJson Project(WorkflowFormDefinition form, CanonicalJson state, WorkflowFieldAudience audience);
}

/// <summary>Applies the same bounded field contract to declarative and custom form renderers.</summary>
public sealed class WorkflowFormService : IWorkflowFormService
{
    private const int MaximumSubmissionBytes = 256 * 1024;
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    public WorkflowFormValidationResult Validate(WorkflowFormDefinition form, string json)
    {
        ArgumentNullException.ThrowIfNull(form);
        CanonicalJson submission;
        try
        {
            submission = CanonicalJson.CreateObject(json, MaximumSubmissionBytes);
        }
        catch (FormatException)
        {
            return Failed(new("$", "presentation.form-invalid-json"));
        }

        var root = submission.ToJsonElement();
        var fields = form.Fields.ToDictionary(field => field.Id.Value, StringComparer.Ordinal);
        var errors = ImmutableArray.CreateBuilder<WorkflowFormError>();
        foreach (var property in root.EnumerateObject())
        {
            if (!fields.ContainsKey(property.Name))
                errors.Add(new(property.Name, "presentation.form-field-unknown"));
        }
        foreach (var field in form.Fields)
        {
            if (!root.TryGetProperty(field.Id.Value, out var value) || value.ValueKind is JsonValueKind.Null)
            {
                if (field.Required) errors.Add(new(field.Id.Value, "presentation.form-field-required"));
                continue;
            }
            ValidateField(field, value, errors);
        }
        return errors.Count == 0
            ? new(true, submission, [])
            : new(false, null, errors.ToImmutable());
    }

    public CanonicalJson Project(
        WorkflowFormDefinition form,
        CanonicalJson state,
        WorkflowFieldAudience audience)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(state);
        if (audience is WorkflowFieldAudience.None || (audience & (audience - 1)) != 0)
            throw new ArgumentException("Exactly one projection audience is required.", nameof(audience));

        var source = state.ToJsonElement();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var field in form.Fields.Where(field => field.ReadableBy.HasFlag(audience))
                         .OrderBy(field => field.Id.Value, StringComparer.Ordinal))
            {
                if (!source.TryGetProperty(field.Id.Value, out var value)) continue;
                writer.WritePropertyName(field.Id.Value);
                value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return CanonicalJson.CreateObject(System.Text.Encoding.UTF8.GetString(stream.ToArray()), MaximumSubmissionBytes);
    }

    private static void ValidateField(
        WorkflowFormField field,
        JsonElement value,
        ImmutableArray<WorkflowFormError>.Builder errors)
    {
        switch (field.Kind)
        {
            case WorkflowFormFieldKind.ShortText:
            case WorkflowFormFieldKind.LongText:
                if (value.ValueKind is not JsonValueKind.String)
                {
                    errors.Add(new(field.Id.Value, "presentation.form-field-text-expected"));
                    return;
                }
                var text = value.GetString()!;
                if (field.MinimumLength is { } minimumLength && text.Length < minimumLength)
                    errors.Add(new(field.Id.Value, "presentation.form-field-too-short"));
                if (field.MaximumLength is { } maximumLength && text.Length > maximumLength)
                    errors.Add(new(field.Id.Value, "presentation.form-field-too-long"));
                if (field.Pattern is { } pattern && !Regex.IsMatch(text, pattern,
                        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, PatternTimeout))
                    errors.Add(new(field.Id.Value, "presentation.form-field-pattern"));
                break;
            case WorkflowFormFieldKind.Date:
                if (value.ValueKind is not JsonValueKind.String ||
                    !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _))
                    errors.Add(new(field.Id.Value, "presentation.form-field-date-expected"));
                break;
            case WorkflowFormFieldKind.Number:
                if (value.ValueKind is not JsonValueKind.Number || !value.TryGetDecimal(out var number))
                {
                    errors.Add(new(field.Id.Value, "presentation.form-field-number-expected"));
                    break;
                }
                if (field.Minimum is { } minimum && number < minimum)
                    errors.Add(new(field.Id.Value, "presentation.form-field-number-too-small"));
                if (field.Maximum is { } maximum && number > maximum)
                    errors.Add(new(field.Id.Value, "presentation.form-field-number-too-large"));
                break;
            case WorkflowFormFieldKind.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    errors.Add(new(field.Id.Value, "presentation.form-field-boolean-expected"));
                break;
            case WorkflowFormFieldKind.Choice:
                if (value.ValueKind is not JsonValueKind.String ||
                    !field.Options.Any(option => string.Equals(option.Value, value.GetString(), StringComparison.Ordinal)))
                    errors.Add(new(field.Id.Value, "presentation.form-field-choice-invalid"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), $"Unsupported field kind '{field.Kind}'.");
        }
    }

    private static WorkflowFormValidationResult Failed(WorkflowFormError error) => new(false, null, [error]);
}

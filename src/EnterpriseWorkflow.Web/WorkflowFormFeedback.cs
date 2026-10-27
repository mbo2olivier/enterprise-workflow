using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Web;

public sealed record WorkflowFormFeedback(IReadOnlyList<WorkflowFormError> Errors, IReadOnlyDictionary<string, string> Values);

/// <summary>Short-lived, single-use feedback bound to its authenticated reader and target page.</summary>
public sealed class WorkflowFormFeedbackStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly object _gate = new();
    private sealed record Entry(IdentityReference Actor, string Path, DateTimeOffset Expires, WorkflowFormFeedback Feedback);
    public string Save(IdentityReference actor, string path, IReadOnlyList<WorkflowFormError> errors, string? code, IFormCollection form)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var budget = 256 * 1024;
        foreach (var item in form.Where(item => item.Key.StartsWith("field.", StringComparison.Ordinal) || item.Key.StartsWith("assignment.", StringComparison.Ordinal) || item.Key == "idempotencyKey"))
        {
            var value = item.Value.ToString();
            var size = System.Text.Encoding.UTF8.GetByteCount(value) + System.Text.Encoding.UTF8.GetByteCount(item.Key);
            if (size > budget || values.Count >= 256) continue;
            values[item.Key] = value; budget -= size;
        }
        var feedback = new WorkflowFormFeedback(errors.Count > 0 ? errors.Take(50).ToArray() : [new("$", code ?? "invalid")], values);
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _entries.Where(pair => pair.Value.Expires <= now)) _entries.TryRemove(pair.Key, out _);
            if (_entries.Count >= 128) _entries.TryRemove(_entries.MinBy(pair => pair.Value.Expires).Key, out _);
            var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
            _entries[key] = new(actor, path, now.AddMinutes(10), feedback);
            return key;
        }
    }
    public WorkflowFormFeedback? Take(string? key, IdentityReference actor, string path)
    {
        if (key is null || !_entries.TryGetValue(key, out var entry) || entry.Actor != actor || entry.Path != path || entry.Expires <= DateTimeOffset.UtcNow) return null;
        return _entries.TryRemove(key, out _) ? entry.Feedback : null;
    }
}

public static class WorkflowValidationMessages
{
    public static string Message(WorkflowFormError error, WorkflowFormDefinition? form = null)
    {
        if (!string.IsNullOrWhiteSpace(error.Message)) return error.Message;
        var field = form?.Fields.FirstOrDefault(field => field.Id.Value == error.FieldId);
        return error.Code switch
        {
            "presentation.form-field-required" => "Ce champ est obligatoire.",
            "presentation.form-field-too-short" => $"Saisissez au moins {field?.MinimumLength} caractères.",
            "presentation.form-field-too-long" => $"Saisissez au maximum {field?.MaximumLength} caractères.",
            "presentation.form-field-pattern" => "La valeur ne respecte pas le format attendu.",
            "presentation.form-field-text-expected" => "Saisissez un texte valide.",
            "presentation.form-field-date-expected" => "Saisissez une date valide.",
            "presentation.form-field-number-expected" => "Saisissez un nombre valide.",
            "presentation.form-field-number-too-small" => $"La valeur doit être supérieure ou égale à {field?.Minimum}.",
            "presentation.form-field-number-too-large" => $"La valeur doit être inférieure ou égale à {field?.Maximum}.",
            "presentation.form-field-choice-invalid" => "Sélectionnez une valeur proposée.",
            "presentation.form-field-boolean-expected" => "Sélectionnez une valeur valide.",
            "presentation.form-file-invalid" or "presentation.form-media-invalid" => "Vérifiez le nombre, le format et la validité des fichiers joints.",
            "runtime.comment-required" => "Un commentaire est obligatoire pour cette action.",
            "validation" => "Le retour de validation a expiré ou a déjà été consulté. Vérifiez le formulaire avant de le renvoyer.",
            "runtime.state-invalid" or "presentation.state-invalid" => "L’état de la demande ne respecte pas les règles du processus.",
            "presentation.designated-assignments-incomplete" or "runtime.undeclared-assignment" or "security.task-assignee-ineligible" => "Sélectionnez un destinataire éligible pour l’étape suivante.",
            "revision" or "conflict" or "store.task-revision-conflict" or "store.instance-revision-conflict" or "runtime.human-task-completion-conflict" or "runtime.human-task-mutation-conflict" => "Cette tâche a été modifiée. Actualisez la page avant de recommencer.",
            "runtime.media-inspection-pending" => "Un fichier est encore en cours de vérification. Réessayez dans un instant.",
            _ => "L’opération n’a pas pu être enregistrée. Vérifiez votre saisie et réessayez.",
        };
    }
}

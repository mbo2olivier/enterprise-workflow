namespace EnterpriseWorkflow.Core.Model;

/// <summary>Immutable snapshot of a module-owned file policy in a published definition.</summary>
public sealed record WorkflowMediaPolicy(string Id, string Label, long MaximumBytes,
    string[] AllowedContentTypes, string[] AllowedExtensions, bool Hidden, string[][] PermissionGroups,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorkflowMediaFormatDefinition[]? Formats = null)
{
    public void Validate()
    {
        foreach (var format in Formats ?? []) format.Validate();
        if (!EnterpriseWorkflow.Abstractions.TechnicalId.IsValid(Id) || string.IsNullOrWhiteSpace(Label) || Label.Length > 200 ||
            MaximumBytes is < 1 or > 104857600 || AllowedContentTypes is not { Length: > 0 and <= 16 } || AllowedExtensions is not { Length: > 0 and <= 16 } ||
            AllowedContentTypes.Any(string.IsNullOrWhiteSpace) || AllowedExtensions.Any(value => string.IsNullOrWhiteSpace(value) || !value.StartsWith('.')) ||
            PermissionGroups is null || PermissionGroups.Length > 32 || PermissionGroups.Any(group => group is null || group.Length == 0 || group.Any(id => !EnterpriseWorkflow.Abstractions.TechnicalId.IsValid(id))))
            throw new FormatException("Invalid module media policy.");
    }
}

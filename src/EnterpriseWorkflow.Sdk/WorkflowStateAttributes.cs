using System.ComponentModel.DataAnnotations;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Persistence;
using System.Reflection;
using System.Text.Json.Serialization;
using EnterpriseWorkflow.Core.Model;

using EnterpriseWorkflow.Runtime;

namespace EnterpriseWorkflow.Sdk;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class StateValidatorAttribute(Type validatorType, params string[] groups) : Attribute
{
    public Type ValidatorType { get; } = validatorType;
    public string[] Groups { get; } = groups;
}


[AttributeUsage(AttributeTargets.Property)]
public sealed class CommentStateAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class HiddenFieldAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class LabelAttribute(string text) : Attribute
{
    public string Text { get; } = text;
}

/// <summary>At least one listed permission must match. Repeated attributes are conjunctive.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class VisibleForAttribute(params string[] permissions) : Attribute
{
    public string[] Permissions { get; } = permissions;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class VisibleForAllAttribute(params string[] permissions) : Attribute
{
    public string[] Permissions { get; } = permissions;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class RequiredForAttribute(params string[] groups) : Attribute
{
    public string[] Groups { get; } = groups;
}

/// <summary>Compiles DTO metadata into a deterministic, CLR-independent state contract.</summary>
public static class WorkflowStateContractCompiler
{
    public static WorkflowStateContract Compile<TState>(int version) => Compile(typeof(TState), version, value => value);

    internal static WorkflowStateContract Compile(Type type, int version, Func<string, string> permission)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        var root = Field(type, string.Empty, false, new HashSet<Type>(), 0);
        if (root.Kind != "object") throw new InvalidOperationException("A state DTO must be an object.");
        var validators = type.GetCustomAttributes<StateValidatorAttribute>().Select(attribute =>
        {
            if (!typeof(IWorkflowStateValidator).IsAssignableFrom(attribute.ValidatorType) || attribute.ValidatorType.IsAbstract)
                throw new InvalidOperationException("A state validator must implement IWorkflowStateValidator.");
            return new WorkflowStateValidatorBinding(attribute.ValidatorType.FullName!, attribute.Groups.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        }).OrderBy(binding => binding.Id, StringComparer.Ordinal).ToArray();
        var contract = new WorkflowStateContract(version, root, validators.Length == 0 ? null : validators);
        contract.EnsureValid();
        return contract;

        WorkflowStateField Field(Type declaredType, string name, bool nullable, HashSet<Type> ancestors, int depth)
        {
            if (depth > 12) throw new InvalidOperationException("State DTO nesting exceeds 12 levels.");
            var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
            var kind = type == typeof(string) ? "string" : type == typeof(bool) ? "boolean" :
                type == typeof(DateOnly) ? "date" : type == typeof(DateTimeOffset) ? "datetime" : type == typeof(Guid) ? "guid" :
                type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ? "integer" :
                type == typeof(decimal) || type == typeof(double) || type == typeof(float) ? "number" : null;
            if (kind is not null) return new() { Name = name, Label = name, Kind = kind, Nullable = nullable || Nullable.GetUnderlyingType(declaredType) is not null };
            var collection = type.IsArray ? type.GetElementType() : type.IsGenericType &&
                (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>) || type.GetGenericTypeDefinition() == typeof(ICollection<>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)) ? type.GenericTypeArguments[0] : null;
            if (collection is not null) return new() { Name = name, Label = name, Kind = "array", Nullable = nullable, PresentationRole = collection == typeof(WorkflowMediaReference) ? "media" : null, Item = Field(collection, "", Nullable.GetUnderlyingType(collection) is not null, ancestors, depth + 1) };
            if (!type.IsClass || type.IsAbstract || type.IsGenericType || !ancestors.Add(type))
                throw new InvalidOperationException($"Unsupported or cyclic state DTO type '{type}'.");
            try
            {
                var nullability = new NullabilityInfoContext();
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if ((properties.Length == 0 && depth > 0) || properties.Length > 128 || properties.Any(property => property.GetIndexParameters().Length != 0))
                    throw new InvalidOperationException("A state DTO supports at most 128 non-indexed properties; nested objects must declare properties.");
                var fields = properties.Select(property =>
                {
                    if (property.GetMethod?.IsPublic != true || property.SetMethod?.IsPublic != true)
                        throw new InvalidOperationException("State DTO properties require public get and set/init accessors for lossless typed round trips.");
                    if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null || property.GetCustomAttribute<JsonConverterAttribute>() is not null)
                        throw new InvalidOperationException("Use HiddenField for internal fields; custom JSON conversion is not supported in state contracts.");
                    var field = Field(property.PropertyType, property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name,
                        nullability.Create(property).ReadState == NullabilityState.Nullable, ancestors, depth + 1);
                    if (string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 128) throw new InvalidOperationException("Invalid persisted state field name.");
                    if (property.IsDefined(typeof(CommentStateAttribute)) && (field.Kind != "array" || field.Item?.Fields.Select(item => item.Name).Contains("text", StringComparer.Ordinal) != true ||
                        !(property.PropertyType.IsArray ? property.PropertyType.GetElementType() == typeof(EnterpriseWorkflow.Abstractions.WorkflowComment) : property.PropertyType.IsGenericType && property.PropertyType.GenericTypeArguments[0] == typeof(EnterpriseWorkflow.Abstractions.WorkflowComment))))
                        throw new InvalidOperationException("CommentState requires a collection of WorkflowComment.");
                    var supported = new[] { typeof(RequiredAttribute), typeof(StringLengthAttribute), typeof(MinLengthAttribute), typeof(MaxLengthAttribute), typeof(RangeAttribute) };
                    if (property.GetCustomAttributes<ValidationAttribute>().Any(attribute => !supported.Contains(attribute.GetType())))
                        throw new InvalidOperationException("Unsupported state validation attribute.");
                    var length = property.GetCustomAttribute<StringLengthAttribute>();
                    var range = property.GetCustomAttribute<RangeAttribute>();
                    if ((length is not null || property.IsDefined(typeof(MinLengthAttribute)) || property.IsDefined(typeof(MaxLengthAttribute))) && field.Kind != "string")
                        throw new InvalidOperationException("Length constraints currently require a string state field.");
                    if (range is not null && field.Kind is not ("number" or "integer"))
                        throw new InvalidOperationException("Range constraints require a numeric state field.");
                    var groups = property.GetCustomAttributes<VisibleForAttribute>().Select(attribute => attribute.Permissions)
                        .Concat(property.GetCustomAttributes<VisibleForAllAttribute>().SelectMany(attribute => attribute.Permissions.Select(item => new[] { item })))
                        .Select(group => group.Length == 0 ? throw new InvalidOperationException("Visibility requires a permission.") : group.Select(permission).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()).ToArray();
                    return field with
                    {
                        PresentationRole = property.IsDefined(typeof(CommentStateAttribute)) ? "comments" : property.PropertyType == typeof(WorkflowMediaReference) ? "media" : field.PresentationRole,
                        Label = property.GetCustomAttribute<LabelAttribute>()?.Text ?? property.Name,
                        Hidden = property.IsDefined(typeof(HiddenFieldAttribute)),
                        Required = property.IsDefined(typeof(RequiredAttribute)),
                        RequiredGroups = property.GetCustomAttributes<RequiredForAttribute>().SelectMany(attribute => attribute.Groups).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                        PermissionGroups = groups,
                        MinimumLength = property.GetCustomAttribute<MinLengthAttribute>()?.Length ?? length?.MinimumLength,
                        MaximumLength = property.GetCustomAttribute<MaxLengthAttribute>()?.Length ?? length?.MaximumLength,
                        Minimum = range is null ? null : Convert.ToDecimal(range.Minimum, System.Globalization.CultureInfo.InvariantCulture),
                        Maximum = range is null ? null : Convert.ToDecimal(range.Maximum, System.Globalization.CultureInfo.InvariantCulture),
                    };
                }).OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
                if (fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length) throw new InvalidOperationException("Duplicate persisted state field name.");
                return new() { Name = name, Label = name, Kind = "object", Nullable = nullable, Fields = fields };
            }
            finally { ancestors.Remove(type); }
        }
    }
}

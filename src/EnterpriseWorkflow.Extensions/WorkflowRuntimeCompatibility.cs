using NuGet.Versioning;

namespace EnterpriseWorkflow.Extensions;

public static class WorkflowRuntimeCompatibility
{
    public static void Validate(WorkflowRuntimeRequirement? requirement, WorkflowModuleLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!NuGetVersion.TryParse(options.RuntimeVersion, out var version) || string.IsNullOrWhiteSpace(options.RuntimeId))
            throw new ArgumentException("The host must declare a valid runtime ID and semantic version.", nameof(options));
        if (requirement is null)
        {
            if (options.RequireRuntimeDeclaration) throw new WorkflowModuleLoadException("EW7055_INCOMPATIBLE_RUNTIME", "This runtime requires a module compatibility declaration.");
            return;
        }
        if (!VersionRange.TryParse(requirement.VersionRange, out var range))
            throw new WorkflowModuleLoadException("EW7054_INVALID_RUNTIME_REQUIREMENT", "Invalid NuGet runtime version range.");
        if (!string.Equals(requirement.Id, options.RuntimeId, StringComparison.Ordinal) || !range.Satisfies(version))
            throw new WorkflowModuleLoadException("EW7055_INCOMPATIBLE_RUNTIME",
                $"Module requires runtime '{requirement.Id}' {requirement.VersionRange}; host provides '{options.RuntimeId}' {options.RuntimeVersion}.");
    }
}

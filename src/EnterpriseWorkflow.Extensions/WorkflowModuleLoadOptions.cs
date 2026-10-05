namespace EnterpriseWorkflow.Extensions;

/// <summary>Bounded startup limits for local trusted module artifacts.</summary>
public sealed class WorkflowModuleLoadOptions
{
    public const int CurrentManifestSchemaVersion = 1;
    public const int CurrentContractVersion = 1;

    public long MaximumManifestBytes { get; set; } = 256 * 1024;
    public long MaximumAssemblyBytes { get; set; } = 128 * 1024 * 1024;
    public long MaximumResourceBytes { get; set; } = 16 * 1024 * 1024;
    public int MaximumDependencies { get; set; } = 256;
    public int MaximumResources { get; set; } = 1024;
    public int MaximumComponents { get; set; } = 256;

    internal void Validate()
    {
        if (MaximumManifestBytes is < 1024 or > 4 * 1024 * 1024) Invalid(nameof(MaximumManifestBytes));
        if (MaximumAssemblyBytes is < 1024 or > 1024L * 1024 * 1024) Invalid(nameof(MaximumAssemblyBytes));
        if (MaximumResourceBytes is < 1 or > 256L * 1024 * 1024) Invalid(nameof(MaximumResourceBytes));
        if (MaximumDependencies is < 0 or > 4096) Invalid(nameof(MaximumDependencies));
        if (MaximumResources is < 0 or > 16384) Invalid(nameof(MaximumResources));
        if (MaximumComponents is < 0 or > 4096) Invalid(nameof(MaximumComponents));
    }

    private static void Invalid(string name) =>
        throw new ArgumentOutOfRangeException(name, "The module loading limit is outside its supported range.");
}

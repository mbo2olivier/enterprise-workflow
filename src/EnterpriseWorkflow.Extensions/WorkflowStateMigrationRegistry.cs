using System.Collections.Immutable;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Extensions;

/// <summary>One exact migration edge supplied by one exact immutable module artifact.</summary>
public sealed record WorkflowStateMigrationRegistration(
    ModuleArtifactReference Artifact,
    int SourceSchemaVersion,
    int TargetSchemaVersion,
    Type MigratorType);

public interface IWorkflowStateMigrationRegistry
{
    bool TryGet(ModuleArtifactReference artifact, int sourceSchemaVersion, int targetSchemaVersion,
        out WorkflowStateMigrationRegistration? registration);
}

/// <summary>Immutable registry built once from the startup module catalog.</summary>
public sealed class WorkflowStateMigrationRegistry : IWorkflowStateMigrationRegistry
{
    private readonly ImmutableDictionary<MigrationKey, WorkflowStateMigrationRegistration> _registrations;

    private WorkflowStateMigrationRegistry(ImmutableDictionary<MigrationKey, WorkflowStateMigrationRegistration> registrations) =>
        _registrations = registrations;

    public ImmutableArray<WorkflowStateMigrationRegistration> Registrations =>
        _registrations.Values.OrderBy(item => item.Artifact.Id.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Artifact.Version, StringComparer.Ordinal)
            .ThenBy(item => item.SourceSchemaVersion).ThenBy(item => item.TargetSchemaVersion).ToImmutableArray();

    public bool TryGet(ModuleArtifactReference artifact, int sourceSchemaVersion, int targetSchemaVersion,
        out WorkflowStateMigrationRegistration? registration) =>
        _registrations.TryGetValue(new MigrationKey(artifact.Id.Value, artifact.Version, artifact.Sha256,
            sourceSchemaVersion, targetSchemaVersion), out registration);

    internal static WorkflowStateMigrationRegistry Create(WorkflowModuleCatalog catalog)
    {
        var registrations = ImmutableDictionary.CreateBuilder<MigrationKey, WorkflowStateMigrationRegistration>();
        foreach (var module in catalog.Modules)
        {
            var builder = new WorkflowStateMigrationRegistryBuilder();
            try
            {
                module.EntryPoint.ConfigureStateMigrations(builder);
            }
            catch (Exception exception) when (exception is not WorkflowModuleLoadException)
            {
                throw new WorkflowModuleLoadException("EW7016_INVALID_STATE_MIGRATION_DECLARATION",
                    $"Module '{module.Manifest.Id.Value}' version '{module.Manifest.Version}' declared invalid state migrations.", exception);
            }
            var artifact = new ModuleArtifactReference(module.Manifest.Id, module.Manifest.Version, module.ArtifactSha256);
            foreach (var declaration in builder.Declarations)
            {
                var key = new MigrationKey(artifact.Id.Value, artifact.Version, artifact.Sha256,
                    declaration.SourceSchemaVersion, declaration.TargetSchemaVersion);
                if (!registrations.TryAdd(key, new WorkflowStateMigrationRegistration(artifact,
                    declaration.SourceSchemaVersion, declaration.TargetSchemaVersion, declaration.MigratorType)))
                    throw new WorkflowModuleLoadException("EW7017_DUPLICATE_STATE_MIGRATION",
                        $"State migration {declaration.SourceSchemaVersion}->{declaration.TargetSchemaVersion} is duplicated for module '{artifact.Id.Value}' version '{artifact.Version}'.");
            }
        }
        return new WorkflowStateMigrationRegistry(registrations.ToImmutable());
    }

    private readonly record struct MigrationKey(string Id, string Version, string Sha256, int Source, int Target);
}

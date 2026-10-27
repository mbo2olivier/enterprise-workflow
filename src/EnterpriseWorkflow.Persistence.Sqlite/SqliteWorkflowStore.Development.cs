using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Core.Serialization;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseWorkflow.Persistence.Sqlite;

public sealed record DevelopmentArtifactRequirement(WorkflowArtifactReference Artifact, IReadOnlyList<string> Instances);
public sealed partial class SqliteWorkflowStore
{
    public async Task<IReadOnlyList<DevelopmentArtifactRequirement>> ReadDevelopmentRequirementsAsync(CancellationToken token)
    {
        await using var context = _database.CreateDbContext();
        var rows = await (from instance in context.Instances.AsNoTracking()
                          join definition in context.Definitions.AsNoTracking() on new { instance.DefinitionId, Version = instance.DefinitionVersion } equals new { definition.DefinitionId, definition.Version }
                          select new { instance.Id, definition.DefinitionId, definition.Version, definition.Sha256, definition.CanonicalJson }).ToListAsync(token).ConfigureAwait(false);
        var requirements = new Dictionary<WorkflowArtifactReference, List<string>>();
        foreach (var group in rows.GroupBy(row => new { row.DefinitionId, row.Version }))
        {
            var first = group.First();
            var model = PublishedWorkflowDefinitionReader.Read(first.CanonicalJson, first.DefinitionId, first.Version, first.Sha256);
            foreach (var artifact in model.Artifacts)
            {
                if (!requirements.TryGetValue(artifact, out var instances)) requirements.Add(artifact, instances = []);
                instances.AddRange(group.Select(row => row.Id));
            }
        }
        return requirements.Select(pair => new DevelopmentArtifactRequirement(pair.Key, pair.Value)).ToArray();
    }
}

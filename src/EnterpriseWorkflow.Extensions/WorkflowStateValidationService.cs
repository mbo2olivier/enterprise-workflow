using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Extensions;

internal sealed class WorkflowStateValidationService(IWorkflowPresentationCatalog catalog, IServiceScopeFactory scopes, IWorkflowStateMigrationRegistry migrations) : IWorkflowStateValidationService
{
    private WorkflowStateMigrationRegistration? Target(WorkflowDefinition definition, int schemaVersion)
    {
        var matches = migrations.StateContracts.Where(item => item.TargetSchemaVersion == schemaVersion && item.TargetContract is not null &&
            definition.Artifacts.Any(artifact => artifact.Id == item.Artifact.Id && artifact.Version == item.Artifact.Version && artifact.Sha256 == item.Artifact.Sha256))
            .ToArray();
        return matches.Length > 0 && matches.Select(item => System.Text.Json.JsonSerializer.Serialize(item.TargetContract)).Distinct(StringComparer.Ordinal).Count() == 1 ? matches[0] : null;
    }
    public WorkflowStateContract? GetContract(WorkflowDefinition definition, int schemaVersion)
    {
        var declared = WorkflowStateContract.From(definition);
        return declared?.Version == schemaVersion ? declared : Target(definition, schemaVersion)?.TargetContract;
    }
    public async ValueTask<bool> ValidateAsync(WorkflowDefinition definition, WorkflowStateValidationContext context, CancellationToken cancellationToken)
    {
        var contract = GetContract(definition, context.State.SchemaVersion);
        if (contract is null) return false;
        if (contract.Version != context.State.SchemaVersion || contract.Validate(context.State.Value, context.Groups).Count > 0) return false;
        if (!catalog.TryGetProcess(definition.Id.Value, definition.Version, out var process) || process is null || process.Definition.Sha256 != definition.Sha256) return false;
        await using var scope = scopes.CreateAsyncScope();
        foreach (var binding in contract.Validators ?? [])
        {
            if (binding.Groups.Length > 0 && !binding.Groups.Any(context.Groups.Contains)) continue;
            var type = (contract.Version == WorkflowStateContract.From(definition)?.Version ? process.StateValidatorTypes : Target(definition, contract.Version)?.TargetValidatorTypes)?.SingleOrDefault(type => type.FullName == binding.Id);
            if (type is null) return false;
            var validator = (IWorkflowStateValidator)ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, type);
            if ((await validator.ValidateAsync(context, cancellationToken)).Count > 0) return false;
        }
        return true;
    }
}

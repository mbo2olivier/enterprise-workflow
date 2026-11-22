namespace EnterpriseWorkflow.Extensions;

/// <summary>Compatibility with a complete runtime distribution, using NuGet version range syntax.</summary>
public sealed record WorkflowRuntimeRequirement(string Id, string VersionRange);

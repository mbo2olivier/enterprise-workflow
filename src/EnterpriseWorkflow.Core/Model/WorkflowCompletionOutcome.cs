namespace EnterpriseWorkflow.Core.Model;

/// <summary>Business result declared by a terminal node, separate from technical execution status.</summary>
public enum WorkflowCompletionOutcome { Succeeded, Declined, Cancelled }

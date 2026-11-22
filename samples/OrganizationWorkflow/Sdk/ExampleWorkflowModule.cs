using EnterpriseWorkflow.Extensions;
namespace Example.Workflow;

/// <summary>One compatibility requirement for the complete example runtime.</summary>
public abstract class ExampleWorkflowModule : WorkflowModule
{
    public override WorkflowRuntimeRequirement RuntimeRequirement => new("Example.Workflow", "[1.0.0-rc.1,2.0.0)");
}

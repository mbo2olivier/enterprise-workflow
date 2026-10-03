namespace EnterpriseWorkflow.Runtime;

/// <summary>Executes one service-node attempt outside the store transaction.</summary>
public interface IServiceNodeHandler
{
    /// <summary>Executes the business operation and returns a bounded result.</summary>
    ValueTask<ServiceNodeResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken);
}

/// <summary>Evaluates one exclusive-decision attempt outside the store transaction.</summary>
public interface IDecisionNodeHandler
{
    /// <summary>Evaluates the decision and returns one named outcome or a bounded failure.</summary>
    ValueTask<DecisionNodeResult> EvaluateAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken);
}

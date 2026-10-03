using EnterpriseWorkflow.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Creates a fresh dependency-injection scope for one handler attempt.</summary>
public interface IScopedWorkflowHandlerResolver
{
    /// <summary>Resolves an exact service handler in a new scope.</summary>
    WorkflowHandlerScope<IServiceNodeHandler> ResolveService(WorkflowHandlerReference reference);

    /// <summary>Resolves an exact decision handler in a new scope.</summary>
    WorkflowHandlerScope<IDecisionNodeHandler> ResolveDecision(WorkflowHandlerReference reference);
}

/// <summary>Owns a resolved handler and its short-lived dependency-injection scope.</summary>
public sealed class WorkflowHandlerScope<THandler> : IDisposable, IAsyncDisposable
    where THandler : class
{
    private IServiceScope? _scope;

    internal WorkflowHandlerScope(IServiceScope scope, THandler handler)
    {
        _scope = scope;
        Handler = handler;
    }

    /// <summary>Gets the handler owned by this attempt scope.</summary>
    public THandler Handler { get; }

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref _scope, null)?.Dispose();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var scope = Interlocked.Exchange(ref _scope, null);
        if (scope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            scope?.Dispose();
        }
    }
}

internal sealed class ScopedWorkflowHandlerResolver(
    IServiceScopeFactory scopeFactory,
    IWorkflowHandlerRegistry registry) : IScopedWorkflowHandlerResolver
{
    public WorkflowHandlerScope<IServiceNodeHandler> ResolveService(WorkflowHandlerReference reference) =>
        Resolve<IServiceNodeHandler>(reference, WorkflowHandlerKind.Service);

    public WorkflowHandlerScope<IDecisionNodeHandler> ResolveDecision(WorkflowHandlerReference reference) =>
        Resolve<IDecisionNodeHandler>(reference, WorkflowHandlerKind.Decision);

    private WorkflowHandlerScope<THandler> Resolve<THandler>(
        WorkflowHandlerReference reference,
        WorkflowHandlerKind expectedKind)
        where THandler : class
    {
        if (!registry.TryGet(reference, out var descriptor))
        {
            throw new WorkflowHandlerResolutionException(
                "EW4002_HANDLER_NOT_FOUND",
                $"Handler '{reference.Id.Value}' version {reference.Version} is not registered.");
        }

        if (descriptor!.Kind != expectedKind)
        {
            throw new WorkflowHandlerResolutionException(
                "EW4003_HANDLER_KIND_MISMATCH",
                $"Handler '{reference.Id.Value}' version {reference.Version} is registered as {descriptor.Kind}, not {expectedKind}.");
        }

        var scope = scopeFactory.CreateScope();
        try
        {
            var handler = scope.ServiceProvider.GetRequiredService(descriptor.HandlerType);
            if (handler is not THandler typedHandler)
            {
                throw new WorkflowHandlerResolutionException(
                    "EW4004_HANDLER_TYPE_MISMATCH",
                    $"Handler type '{descriptor.HandlerType.FullName}' does not implement {typeof(THandler).Name}.");
            }

            return new WorkflowHandlerScope<THandler>(scope, typedHandler);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}

/// <summary>Reports an exact missing or incompatible runtime binding without fallback.</summary>
public sealed class WorkflowHandlerResolutionException : InvalidOperationException
{
    /// <summary>Creates a deterministic resolution failure.</summary>
    public WorkflowHandlerResolutionException(string code, string message)
        : base($"{code}: {message}") => Code = code;

    /// <summary>Gets the stable diagnostic code.</summary>
    public string Code { get; }
}

using EnterpriseWorkflow.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Runtime;

internal sealed class WorkflowWorkerHostedService(
    IWorkflowExecutionPump pump,
    IOptions<WorkflowRuntimeOptions> configuredOptions,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly WorkflowRuntimeOptions _options = configuredOptions.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        Enumerable.Range(0, _options.WorkerConcurrency).Select(index => RunAsync(index, stoppingToken)));

    private async Task RunAsync(int index, CancellationToken token)
    {
        var owner = new TechnicalId($"workflow.worker.{Guid.NewGuid():N}.{index}");
        while (!token.IsCancellationRequested)
        {
            bool handled;
            try
            {
                handled = await pump.ExecuteNextAsync(owner, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (WorkflowRuntimeException)
            {
                handled = false;
            }
            if (!handled) await Task.Delay(_options.IdlePollingInterval, timeProvider, token).ConfigureAwait(false);
        }
    }
}

internal sealed class WorkflowOutboxHostedService(
    IWorkflowOutboxPump pump,
    IOptions<WorkflowRuntimeOptions> configuredOptions,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly WorkflowRuntimeOptions _options = configuredOptions.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        Enumerable.Range(0, _options.OutboxConcurrency).Select(index => RunAsync(index, stoppingToken)));

    private async Task RunAsync(int index, CancellationToken token)
    {
        var owner = new TechnicalId($"workflow.outbox.{Guid.NewGuid():N}.{index}");
        while (!token.IsCancellationRequested)
        {
            bool handled;
            try
            {
                handled = await pump.DeliverNextAsync(owner, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (WorkflowRuntimeException)
            {
                handled = false;
            }
            if (!handled) await Task.Delay(_options.OutboxIdlePollingInterval, timeProvider, token).ConfigureAwait(false);
        }
    }
}

using System.Diagnostics;

namespace EnterpriseWorkflow.Web.Playwright.Tests;

/// <summary>Waits for both the restarted capture API and the expected delivery, with one bounded deadline.</summary>
internal static class SmtpCaptureProbe
{
    internal static async Task<string> WaitForMessagesAsync(HttpClient client, string captureUrl,
        Func<string, bool> matches, TimeSpan timeout, TimeSpan interval, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        Exception? lastFailure = null;
        var apiResponded = false;
        while (elapsed.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            request.CancelAfter(remaining);
            try
            {
                var messages = await client.GetStringAsync(captureUrl + "/api/v1/messages", request.Token);
                apiResponded = true;
                if (matches(messages)) return messages;
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null || (int)exception.StatusCode >= 500)
            {
                // docker compose start completes before Mailpit's HTTP listener is ready.
                lastFailure = exception;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = exception;
            }
            remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(interval < remaining ? interval : remaining, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException($"SMTP capture did not return the expected notification within {timeout.TotalSeconds}s. " +
            $"API responded successfully: {apiResponded}. Last transport failure: {lastFailure?.Message ?? "none"}.", lastFailure);
    }
}

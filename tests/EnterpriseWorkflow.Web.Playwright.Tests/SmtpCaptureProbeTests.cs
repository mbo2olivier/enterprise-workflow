using System.Net;
using Xunit;

namespace EnterpriseWorkflow.Web.Playwright.Tests;

public sealed class SmtpCaptureProbeTests
{
    [Fact]
    public async Task RestartResetAndServiceUnavailableDoNotSkipTheDeliveryCheck()
    {
        var calls = 0;
        using var handler = new ProbeHandler((_, _) =>
        {
            calls++;
            return calls switch
            {
                1 => throw new HttpRequestException("Connection reset by peer", new IOException("transport reset")),
                2 => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
                3 => Task.FromResult(Response("unrelated@example.test")),
                _ => Task.FromResult(Response("bob@example.test /tasks/expected")),
            };
        });
        using var client = new HttpClient(handler);
        var messages = await SmtpCaptureProbe.WaitForMessagesAsync(client, "http://localhost",
            content => content.Contains("bob@example.test", StringComparison.Ordinal) && content.Contains("/tasks/expected", StringComparison.Ordinal),
            TimeSpan.FromSeconds(5), TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.Equal(4, calls);
        Assert.Contains("/tasks/expected", messages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthyApiWithoutExpectedMailStillFailsAtTheDeadline()
    {
        using var handler = new ProbeHandler((_, _) => Task.FromResult(Response("no matching notification")));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<TimeoutException>(() => SmtpCaptureProbe.WaitForMessagesAsync(client, "http://localhost",
            _ => false, TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken));
        Assert.Contains("API responded successfully: True", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientErrorsAreNotHiddenAsTransientStartupFailures()
    {
        using var handler = new ProbeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => SmtpCaptureProbe.WaitForMessagesAsync(client, "http://localhost",
            _ => true, TimeSpan.FromSeconds(5), TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public async Task StalledApiIsBoundedAndExternalCancellationIsPreserved()
    {
        using var handler = new ProbeHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response("");
        });
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<TimeoutException>(() => SmtpCaptureProbe.WaitForMessagesAsync(client, "http://localhost",
            _ => true, TimeSpan.FromMilliseconds(30), TimeSpan.Zero, TestContext.Current.CancellationToken));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SmtpCaptureProbe.WaitForMessagesAsync(client, "http://localhost",
            _ => true, TimeSpan.FromSeconds(5), TimeSpan.Zero, cancellation.Token));
    }

    private static HttpResponseMessage Response(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content) };
    private sealed class ProbeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}

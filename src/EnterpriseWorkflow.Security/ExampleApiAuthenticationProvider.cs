using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseWorkflow.Security;

public sealed class ExampleApiAuthenticationOptions
{
    public required string ProviderId { get; init; }
    public required Uri BaseAddress { get; init; }
    public string AuthenticationPath { get; init; } = "api/authenticate";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public int MaximumResponseBytes { get; init; } = 65_536;
    public bool EnablePasswordCredentialForwarding { get; init; }

    public void Validate()
    {
        _ = new IdentityReference(ProviderId, "validation");
        if (!BaseAddress.IsAbsoluteUri || BaseAddress.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The API base address must be an absolute HTTPS URI.");
        if (Uri.TryCreate(AuthenticationPath, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(AuthenticationPath))
            throw new InvalidOperationException("AuthenticationPath must be a non-empty relative URI.");
        if (Timeout <= TimeSpan.Zero || MaximumResponseBytes is < 1024 or > 10_485_760)
            throw new InvalidOperationException("API timeout or response limit is invalid.");
        if (!EnablePasswordCredentialForwarding)
            throw new InvalidOperationException("Password forwarding to the configured API must be explicitly enabled.");
    }
}

public sealed class ExampleApiAuthenticationProvider : IAuthenticationProvider, IDisposable
{
    private readonly ExampleApiAuthenticationOptions _options;
    private readonly HttpClient _client;

    public ExampleApiAuthenticationProvider(ExampleApiAuthenticationOptions options, HttpMessageHandler? handler = null)
    {
        options.Validate();
        _options = options;
        handler ??= new SocketsHttpHandler { AllowAutoRedirect = false };
        _client = new HttpClient(handler, disposeHandler: true) { BaseAddress = options.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
    }

    public string ProviderId => _options.ProviderId;

    public async ValueTask<AuthenticationResult> AuthenticateAsync(PasswordCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (string.IsNullOrWhiteSpace(credential.UserName) || string.IsNullOrEmpty(credential.Password))
            return new(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.AuthenticationPath)
            {
                Content = JsonContent.Create(new ApiCredential(credential.UserName, credential.Password)),
            };
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400) return new(AuthenticationStatus.Unavailable, ErrorCode: "security.api-redirect-refused");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");
            if (!response.IsSuccessStatusCode) return new(AuthenticationStatus.Unavailable, ErrorCode: "security.api-unavailable");
            if (response.Content.Headers.ContentLength > _options.MaximumResponseBytes)
                return new(AuthenticationStatus.Unavailable, ErrorCode: "security.api-response-too-large");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<ApiAuthenticationResponse>(
                new BoundedReadStream(stream, _options.MaximumResponseBytes), cancellationToken: timeout.Token).ConfigureAwait(false);
            return result is { Authenticated: true } && !string.IsNullOrWhiteSpace(result.SubjectId)
                ? new(AuthenticationStatus.Succeeded, new IdentityReference(ProviderId, result.SubjectId))
                : new(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(AuthenticationStatus.Unavailable, ErrorCode: "security.api-timeout");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException)
        {
            return new(AuthenticationStatus.Unavailable, ErrorCode: "security.api-unavailable");
        }
    }

    public void Dispose() => _client.Dispose();
    private sealed record ApiCredential(string UserName, string Password);
    private sealed record ApiAuthenticationResponse(
        [property: JsonPropertyName("authenticated")] bool Authenticated,
        [property: JsonPropertyName("subjectId")] string? SubjectId);

    private sealed class BoundedReadStream(Stream inner, long maximumBytes) : Stream
    {
        private long _read;
        public override bool CanRead => inner.CanRead; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => throw new NotSupportedException(); public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { var read = inner.Read(buffer, offset, count); Count(read); return read; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); Count(read); return read; }
        private void Count(int value) { _read += value; if (_read > maximumBytes) throw new IOException("API response exceeded the configured limit."); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}

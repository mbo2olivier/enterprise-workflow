namespace EnterpriseWorkflow.Runtime;

/// <summary>Private immutable file objects. Configure a shared durable volume when running multiple hosts.</summary>
public sealed class FileWorkflowMediaStorageProvider : IWorkflowMediaStorageProvider
{
    private readonly string _root;
    public FileWorkflowMediaStorageProvider(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(_root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    private string PathFor(string key)
    {
        if (key.Length is < 1 or > 128 || key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')) throw new ArgumentException("Invalid private object key.", nameof(key));
        return Path.Combine(_root, key);
    }
    public async ValueTask WriteAsync(string objectKey, Stream source, long maximumBytes, CancellationToken cancellationToken)
    {
        var destination = PathFor(objectKey);
        var temporary = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                var buffer = new byte[81920];
                long length = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    length += read;
                    if (length > maximumBytes) throw new InvalidDataException("File exceeds media size limit.");
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await file.FlushAsync(cancellationToken);
            }
            try { File.Move(temporary, destination, overwrite: false); }
            catch (IOException) when (File.Exists(destination)) { /* Content-addressed object already durable. */ }
        }
        finally { File.Delete(temporary); }
    }
    public ValueTask<Stream> OpenAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Stream>(new FileStream(PathFor(objectKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));
    }
    public ValueTask DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(PathFor(objectKey));
        return ValueTask.CompletedTask;
    }
}

namespace CouchPad.Platform.Windows;

/// <summary>
/// Holds a per-user, cross-process, cooperative lock for CCD mutations. The
/// caller must use the same path in every writer and must not delete the file.
/// This lock does not supply transaction fencing or stop a blocked native call.
/// </summary>
public sealed class DisplayMutationLock : IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(50);
    private FileStream? _handle;

    private DisplayMutationLock(FileStream handle) => _handle = handle;

    /// <summary>
    /// Waits until the exclusive handle can be obtained or cancellation is
    /// requested. Only sharing/lock violations are retried; other I/O errors
    /// propagate, so the caller can fail closed.
    /// </summary>
    public static async ValueTask<DisplayMutationLock> AcquireAsync(
        string lockFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

        var path = Path.GetFullPath(lockFilePath);
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Missing lock directory.", nameof(lockFilePath));
        Directory.CreateDirectory(directory);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var handle = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.None, bufferSize: 1, FileOptions.None);
                if (cancellationToken.IsCancellationRequested)
                {
                    handle.Dispose();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return new DisplayMutationLock(handle);
            }
            catch (IOException error) when (IsLockContention(error))
            {
                await Task.Delay(RetryInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsLockContention(IOException error)
    {
        var code = unchecked((uint)error.HResult) & 0xffff;
        return code is 32 or 33; // ERROR_SHARING_VIOLATION / ERROR_LOCK_VIOLATION
    }

    public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Dispose();

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

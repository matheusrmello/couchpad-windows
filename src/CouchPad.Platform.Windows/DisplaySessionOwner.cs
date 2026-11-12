namespace CouchPad.Platform.Windows;

/// <summary>Exclusive, per-journal owner held by the App for its process lifetime.</summary>
public sealed class DisplaySessionOwner : IDisposable
{
    private FileStream? _handle;
    private DisplaySessionOwner(FileStream handle) => _handle = handle;

    public static DisplaySessionOwner Acquire(string journalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var directory = Path.GetDirectoryName(Path.GetFullPath(journalPath))!;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "display-session.owner.lock");
        return new DisplaySessionOwner(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
    }

    public static async Task<(DisplaySessionOwner Owner, bool Waited)> AcquireWhenAvailableAsync(
        string journalPath, CancellationToken cancellationToken)
    {
        var waited = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return (Acquire(journalPath), waited); }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33)
            {
                waited = true;
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Dispose();
}

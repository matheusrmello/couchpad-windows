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

    public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Dispose();
}

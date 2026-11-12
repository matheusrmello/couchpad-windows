using System.Diagnostics;
using System.Globalization;

namespace CouchPad.Platform.Windows;

/// <summary>Starts a short-lived, isolated Windows notification process. Failures never block automation.</summary>
public sealed class WindowsCountdownNotifications : IDisposable
{
    private readonly string _helperPath;
    private readonly object _sync = new();
    private CancellationTokenSource? _current;
    private bool _disposed;

    public WindowsCountdownNotifications(string helperPath) => _helperPath = Path.GetFullPath(helperPath);

    public void Show(TimeSpan duration, string title, string remainingFormat)
    {
        if (duration <= TimeSpan.Zero) { Cancel(); return; }
        CancellationTokenSource source;
        lock (_sync)
        {
            if (_disposed) return;
            _current?.Cancel();
            source = new CancellationTokenSource();
            _current = source;
        }
        var deadline = DateTimeOffset.UtcNow + duration;
        _ = Task.Run(async () =>
        {
            try
            {
                if (source.IsCancellationRequested || !File.Exists(_helperPath)) return;
                var info = new ProcessStartInfo(_helperPath) {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                    WorkingDirectory = Path.GetDirectoryName(_helperPath)!,
                };
                info.ArgumentList.Add(deadline.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
                info.ArgumentList.Add(duration.TotalSeconds.ToString(CultureInfo.InvariantCulture));
                info.ArgumentList.Add(title);
                info.ArgumentList.Add(remainingFormat);
                using var process = Process.Start(info);
                if (process is null) return;
                using var registration = source.Token.Register(() => { try { process.StandardInput.Close(); } catch (Exception) { } });
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception) { /* Notification failures must not affect automation. */ }
            finally
            {
                lock (_sync) { if (ReferenceEquals(_current, source)) _current = null; }
                source.Dispose();
            }
        });
    }

    public void Cancel()
    {
        lock (_sync) { _current?.Cancel(); }
    }

    public void Dispose()
    {
        lock (_sync) { if (_disposed) return; _disposed = true; _current?.Cancel(); }
    }
}

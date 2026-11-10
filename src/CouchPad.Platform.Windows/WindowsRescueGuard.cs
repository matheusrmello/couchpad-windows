using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Starts an independent rescue executable and holds its transaction pipe.</summary>
public sealed class WindowsRescueGuard : IRescueGuard
{
    private readonly string _executable;
    private readonly string _journal;
    private readonly TimeSpan _timeout;
    private readonly bool _allowRecovery;

    public WindowsRescueGuard(string executable, string journal, TimeSpan timeout, bool allowRecovery = false)
    {
        _executable = Path.GetFullPath(executable);
        _journal = Path.GetFullPath(journal);
        if (timeout < TimeSpan.FromSeconds(5) || timeout > TimeSpan.FromMinutes(10)) throw new ArgumentOutOfRangeException(nameof(timeout));
        _timeout = timeout;
        _allowRecovery = allowRecovery;
    }

    public async Task<IRescueLease> BeginAsync(string operation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_allowRecovery) throw new NotSupportedException("Display recovery is disabled.");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var pipeName = "CouchPad-rescue-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Process? child = null;
        try
        {
            using var parent = Process.GetCurrentProcess();
            var info = new ProcessStartInfo(_executable) { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add(pipeName);
            info.ArgumentList.Add(_journal);
            info.ArgumentList.Add(parent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            info.ArgumentList.Add(parent.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            info.ArgumentList.Add(((int)_timeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
            child = Process.Start(info) ?? throw new InvalidOperationException("Rescue process did not start.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await pipe.WaitForConnectionAsync(deadline.Token).ConfigureAwait(false);
            var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            if (await reader.ReadLineAsync(deadline.Token).ConfigureAwait(false) != "READY") throw new InvalidOperationException("Rescue did not become ready.");
            return new Lease(pipe, reader, writer, child);
        }
        catch
        {
            pipe.Dispose();
            child?.Dispose();
            throw;
        }
    }

    private sealed class Lease : IRescueLease
    {
        private readonly NamedPipeServerStream _pipe;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly Process _child;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly CancellationTokenSource _failure = new();
        private int _releaseStarted;
        private bool _finished, _abandoned, _disposed;
        public Lease(NamedPipeServerStream pipe, StreamReader reader, StreamWriter writer, Process child)
        {
            _pipe = pipe; _reader = reader; _writer = writer; _child = child;
            _ = MonitorChildAsync();
        }
        public CancellationToken FailureToken => _failure.Token;
        public void ThrowIfUnavailable()
        {
            if (_child.HasExited) _failure.Cancel();
            if (_failure.IsCancellationRequested) throw new InvalidOperationException("Rescue watchdog unavailable.");
        }
        private async Task MonitorChildAsync()
        {
            try
            {
                await _child.WaitForExitAsync().ConfigureAwait(false);
                if (Volatile.Read(ref _releaseStarted) == 0) _failure.Cancel();
            }
            catch (Exception)
            {
                if (Volatile.Read(ref _releaseStarted) == 0) _failure.Cancel();
            }
        }
        public Task CommitAsync(CancellationToken cancellationToken = default) => SignalAsync("COMMIT", cancellationToken);
        public Task AbortAsync(CancellationToken cancellationToken = default) => SignalAsync("ABORT", cancellationToken);
        private async Task SignalAsync(string command, CancellationToken token)
        {
            await _gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_disposed || _abandoned || _finished) throw new InvalidOperationException("Rescue lease is not active.");
                // Once delivery starts, caller cancellation cannot make the outcome
                // ambiguous by interrupting the ACK read halfway through the signal.
                token.ThrowIfCancellationRequested();
                ThrowIfUnavailable();
                Interlocked.Exchange(ref _releaseStarted, 1);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _writer.WriteLineAsync(command.AsMemory(), deadline.Token).ConfigureAwait(false);
                await _writer.FlushAsync(deadline.Token).ConfigureAwait(false);
                if (await _reader.ReadLineAsync(deadline.Token).ConfigureAwait(false) != "ACK") throw new IOException("Rescue did not acknowledge completion.");
                _finished = true;
            }
            finally { _gate.Release(); }
        }
        public void Abandon() => _abandoned = true;
        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            // Keep an unfinished pipe alive: disposal itself must not request restoration.
            if (_finished) Cleanup();
            else _ = CleanupAfterExitAsync();
            return ValueTask.CompletedTask;
        }
        private async Task CleanupAfterExitAsync() { try { await _child.WaitForExitAsync().ConfigureAwait(false); } catch (InvalidOperationException) { } finally { Cleanup(); } }
        private void Cleanup() { _reader.Dispose(); _writer.Dispose(); _pipe.Dispose(); _child.Dispose(); }
    }
}

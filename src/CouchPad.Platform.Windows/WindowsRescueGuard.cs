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
    private readonly bool _allowExperimentalAudioRecovery;
    private readonly RescueTransactionContext? _transaction;

    public WindowsRescueGuard(string executable, string journal, TimeSpan timeout, bool allowRecovery = false,
        RescueTransactionContext? transaction = null, bool allowExperimentalAudioRecovery = false)
    {
        _executable = Path.GetFullPath(executable);
        _journal = Path.GetFullPath(journal);
        if (timeout < TimeSpan.FromSeconds(5) || timeout > TimeSpan.FromMinutes(10)) throw new ArgumentOutOfRangeException(nameof(timeout));
        _timeout = timeout;
        _allowRecovery = allowRecovery;
        _allowExperimentalAudioRecovery = allowExperimentalAudioRecovery;
        _transaction = transaction;
    }

    public async Task<IRescueLease> BeginAsync(string operation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_allowRecovery) throw new NotSupportedException("Display recovery is disabled.");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var journal = new JsonSessionJournalStore(_journal).Load()
            ?? throw new InvalidDataException("Recovery journal is unavailable.");
        if (journal.State is not (SessionJournalEntry.PendingState or SessionJournalEntry.ActiveState))
            throw new InvalidDataException("Recovery journal has an invalid state.");
        if (journal.RestoreAudioBaseline is not null && !_allowExperimentalAudioRecovery)
            throw new NotSupportedException("This rescue guard cannot yet restore real audio.");
        var baselineHash = RescueDecisionStore.ComputeBaselineSha256(journal);
        var transactionId = Guid.NewGuid();
        var decisionPath = Path.Combine(Path.GetDirectoryName(_journal)!, "rescue-decisions", transactionId.ToString("N") + ".json");
        var decisions = new RescueDecisionStore(decisionPath);
        var armed = await decisions.InitializeAsync(transactionId, baselineHash, cancellationToken).ConfigureAwait(false);
        if (armed.State != RescueDecisionState.Armed) throw new InvalidDataException("Rescue decision is not armed.");
        var pipeName = "CouchPad-rescue-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Process? child = null;
        var bound = false;
        try
        {
            using var parent = Process.GetCurrentProcess();
            var info = new ProcessStartInfo(_executable) { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add(pipeName);
            info.ArgumentList.Add(_journal);
            info.ArgumentList.Add(parent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            info.ArgumentList.Add(parent.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            info.ArgumentList.Add(((int)_timeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
            info.ArgumentList.Add(transactionId.ToString("D"));
            info.ArgumentList.Add(baselineHash);
            info.ArgumentList.Add(decisionPath);
            child = Process.Start(info) ?? throw new InvalidOperationException("Rescue process did not start.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await pipe.WaitForConnectionAsync(deadline.Token).ConfigureAwait(false);
            var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            if (await reader.ReadLineAsync(deadline.Token).ConfigureAwait(false) != "READY") throw new InvalidOperationException("Rescue did not become ready.");
            var afterReady = await decisions.ReadAsync(transactionId, baselineHash, deadline.Token).ConfigureAwait(false);
            if (afterReady?.State != RescueDecisionState.Armed)
                throw new RescueDecisionUncertainException("Rescue is no longer armed.");
            _transaction?.Bind(decisionPath, transactionId, baselineHash);
            bound = _transaction is not null;
            return new Lease(pipe, reader, writer, child, decisions, transactionId, baselineHash, _transaction);
        }
        catch (Exception ex)
        {
            pipe.Dispose();
            var childStarted = child is not null;
            child?.Dispose();
            if (bound) _transaction!.Release(transactionId);
            if (childStarted)
                throw new RescueDecisionUncertainException("Rescue startup outcome is uncertain.", ex);
            throw;
        }
    }

    private sealed class Lease : IRescueLease
    {
        private readonly NamedPipeServerStream _pipe;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly Process _child;
        private readonly RescueDecisionStore _decisions;
        private readonly Guid _transactionId;
        private readonly string _baselineHash;
        private readonly RescueTransactionContext? _transaction;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly CancellationTokenSource _failure = new();
        private int _releaseStarted;
        private bool _finished, _abandoned, _disposed;
        public Lease(NamedPipeServerStream pipe, StreamReader reader, StreamWriter writer, Process child,
            RescueDecisionStore decisions, Guid transactionId, string baselineHash, RescueTransactionContext? transaction)
        {
            _pipe = pipe; _reader = reader; _writer = writer; _child = child;
            _decisions = decisions; _transactionId = transactionId; _baselineHash = baselineHash;
            _transaction = transaction;
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
                var desired = command == "COMMIT" ? RescueDecisionState.Committed : RescueDecisionState.Aborted;
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _writer.WriteLineAsync(command.AsMemory(), deadline.Token).ConfigureAwait(false);
                    await _writer.FlushAsync(deadline.Token).ConfigureAwait(false);
                    if (await _reader.ReadLineAsync(deadline.Token).ConfigureAwait(false) != "ACK") throw new IOException("Rescue did not acknowledge completion.");
                    if (!await IsDecisionAsync(desired).ConfigureAwait(false)) throw new InvalidDataException("Rescue acknowledgement has no matching decision.");
                    Finish();
                }
                catch (Exception ex)
                {
                    // Delivery can succeed even if the response was lost. Reconcile
                    // with the durable decision rather than initiating a second mutation.
                    try
                    {
                        if (await IsDecisionAsync(desired).ConfigureAwait(false)) { Finish(); return; }
                    }
                    catch (Exception readError)
                    {
                        throw new RescueDecisionUncertainException("Rescue decision could not be read.", new AggregateException(ex, readError));
                    }
                    throw new RescueDecisionUncertainException("Rescue decision is not confirmed.", ex);
                }
            }
            finally { _gate.Release(); }
        }
        private async Task<bool> IsDecisionAsync(RescueDecisionState desired)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var record = await _decisions.ReadAsync(_transactionId, _baselineHash, deadline.Token).ConfigureAwait(false);
            return record?.State == desired;
        }
        private void Finish()
        {
            _transaction?.Release(_transactionId);
            _finished = true;
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

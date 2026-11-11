using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CouchPad.Core.Automation;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (args.Length != 8 || !OperatingSystem.IsWindows()) return 2;
var pipeName = args[0];
var journalPath = Path.GetFullPath(args[1]);
if (!int.TryParse(args[2], out var parentId) || !long.TryParse(args[3], out var startedTicks) ||
    !int.TryParse(args[4], out var timeoutMs) || timeoutMs < 5000 || timeoutMs > 600000 ||
    !Guid.TryParse(args[5], out var transactionId) || transactionId == Guid.Empty) return 2;
var baselineHash = args[6];
var decisionPath = Path.GetFullPath(args[7]);
var expectedDecisionPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(journalPath)!, "rescue-decisions", transactionId.ToString("N") + ".json"));
if (!string.Equals(decisionPath, expectedDecisionPath, StringComparison.OrdinalIgnoreCase)) return 2;
var decisions = new RescueDecisionStore(decisionPath);
var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName + "|" + journalPath.ToUpperInvariant())));
var lockPath = Path.Combine(Path.GetDirectoryName(journalPath)!, "watchdog-" + identity + ".lock");
FileStream instanceLock;
try { instanceLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
catch (IOException) { return 3; }
using (instanceLock)
{
    var stage = "journal";
    try
    {
        var journal = new JsonSessionJournalStore(journalPath).Load();
        if (journal is null || (journal.State != SessionJournalEntry.PendingState && journal.State != SessionJournalEntry.ActiveState) || journal.RestoreDisplay?.Topology is null)
            throw new InvalidDataException("Recovery journal is unavailable.");
#if !RESCUE_TEST_BACKEND
        if (journal.RestoreAudioBaseline is not null)
            throw new NotSupportedException("This rescue process cannot yet restore real audio.");
#endif
        var actualHash = RescueDecisionStore.ComputeBaselineSha256(journal);
        if (!string.Equals(actualHash, baselineHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Recovery baseline does not match transaction.");
        var snapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(journal.RestoreDisplay.Topology);
        if (snapshot is null || !DisplaySnapshotComparer.SemanticallyEqual(snapshot, snapshot)) throw new InvalidDataException("Recovery topology is invalid.");
        using var decisionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var currentDecision = await decisions.ReadAsync(transactionId, baselineHash, decisionTimeout.Token);
        if (currentDecision?.State != RescueDecisionState.Armed) throw new InvalidDataException("Recovery decision is not armed.");
        Process? parent;
        try { parent = Process.GetProcessById(parentId); }
        catch (ArgumentException) { stage = "restore"; await Recover(decisions, transactionId, baselineHash, decisionPath, journal); return 0; }
        using var parentHandle = parent;
        if (parent.StartTime.ToUniversalTime().Ticks != startedTicks) { stage = "restore"; await Recover(decisions, transactionId, baselineHash, decisionPath, journal); return 0; }
        stage = "connect";
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await pipe.ConnectAsync(connectTimeout.Token);
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
#if RESCUE_TEST_BACKEND
        if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_READY") == "1") return 18;
#endif
        await writer.WriteLineAsync("READY");
#if RESCUE_TEST_BACKEND
        if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_EXIT_AFTER_READY") == "1") return 17;
#endif
        stage = "protocol";
        var signal = reader.ReadLineAsync();
        var parentExit = parent.WaitForExitAsync();
        var timeout = Task.Delay(timeoutMs);
        await Task.WhenAny(signal, parentExit, timeout);
        if (!parentExit.IsCompleted && !timeout.IsCompleted && signal.IsCompletedSuccessfully && signal.Result is "COMMIT" or "ABORT")
        {
#if RESCUE_TEST_BACKEND
            if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_DECISION") == "1") return 19;
#endif
            var desired = signal.Result == "COMMIT" ? RescueDecisionState.Committed : RescueDecisionState.Aborted;
            if (!await decisions.TryTransitionAsync(transactionId, baselineHash, RescueDecisionState.Armed, desired))
            {
                stage = "restore";
                await Recover(decisions, transactionId, baselineHash, decisionPath, journal);
                return 1;
            }
#if RESCUE_TEST_BACKEND
            await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("COUCHPAD_TEST_MARKER")!, signal.Result!);
            if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_DROP_ACK") == "1") return 0;
            var ackGate = Environment.GetEnvironmentVariable("COUCHPAD_TEST_ACK_GATE");
            if (!string.IsNullOrEmpty(ackGate))
            {
                await File.WriteAllTextAsync(ackGate + ".waiting", "waiting");
                using var gateTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                while (!File.Exists(ackGate + ".release")) await Task.Delay(25, gateTimeout.Token);
            }
#endif
            // The persisted terminal decision is authoritative even if ACK is lost.
            try { await writer.WriteLineAsync("ACK"); }
            catch (IOException) { }
            return 0;
        }
        stage = "restore";
        await Recover(decisions, transactionId, baselineHash, decisionPath, journal);
        return 0;
    }
    catch (Exception ex) { RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), stage, ex); return 1; }
}

static async Task Recover(RescueDecisionStore decisions, Guid id, string baselineHash, string decisionPath, SessionJournalEntry journal)
{
    if (!await decisions.TryTransitionAsync(id, baselineHash, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested))
    {
        var current = await decisions.ReadAsync(id, baselineHash);
        if (current?.State is RescueDecisionState.Committed or RescueDecisionState.Aborted or RescueDecisionState.Recovered) return;
        if (current?.State != RescueDecisionState.RecoveryRequested) throw new InvalidDataException("Recovery decision is indeterminate.");
    }
    await Restore(journal, decisionPath, id, baselineHash);
    if (!await decisions.TryTransitionAsync(id, baselineHash, RescueDecisionState.RecoveryRequested, RescueDecisionState.Recovered))
        throw new InvalidDataException("Recovery completion could not be recorded.");
}

static async Task Restore(SessionJournalEntry journal, string decisionPath, Guid id, string baselineHash)
{
    var stateDirectory = Path.GetDirectoryName(Path.GetDirectoryName(decisionPath)!)!;
    var transaction = new RescueTransactionContext(stateDirectory);
    transaction.Bind(decisionPath, id, baselineHash);
#if RESCUE_TEST_BACKEND
    await using var mutation = await DisplayMutationLock.AcquireAsync(transaction.MutationLockPath);
    await transaction.RequireAsync(RescueDecisionState.RecoveryRequested);
    var marker = Environment.GetEnvironmentVariable("COUCHPAD_TEST_MARKER")!;
    await File.WriteAllTextAsync(marker, journal.RestoreAudioBaseline is null ? "RESTORE" : "RESTORE_DISPLAY");
    if (journal.RestoreAudioBaseline is not null)
    {
        AudioRecoveryTransaction.Restore(journal.RestoreAudioBaseline, new FakeAudioRecoveryBackend());
        await File.AppendAllTextAsync(marker, "+AUDIO");
    }
#else
    var display = WindowsDisplayService.ForRecovery(new WindowsDisplayOptions("", 0, 0, AllowWrites: true), transaction);
    await display.RestoreAsync(journal.RestoreDisplay!, CancellationToken.None);
#endif
}

#if RESCUE_TEST_BACKEND
sealed class FakeAudioRecoveryBackend : IAudioRecoveryBackend
{
    private readonly Dictionary<AudioRole, string> _roles = new();
    private readonly Dictionary<string, (float Volume, bool Muted)> _volumes = new();
    public bool IsActive(string endpointId) => true;
    public void SetDefault(string endpointId, AudioRole role)
    {
        if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_AUDIO_FAIL") == "1") throw new InvalidOperationException("Injected audio recovery failure.");
        _roles[role] = endpointId;
    }
    public string? GetDefault(AudioRole role) => _roles.GetValueOrDefault(role);
    public void SetVolumeAndMute(string endpointId, float volume, bool muted) => _volumes[endpointId] = (volume, muted);
    public (float Volume, bool Muted) GetVolumeAndMute(string endpointId) => _volumes[endpointId];
}
#endif

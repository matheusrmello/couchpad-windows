using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using CouchPad.Core.Automation;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

#if RESCUE_TEST_BACKEND
if (args is ["--test-assisted-recovery", var assistedJournal, var resultMarker] && OperatingSystem.IsWindows())
{
    var recovered = await RunAssistedRecovery(assistedJournal);
    File.WriteAllText(resultMarker, recovered ? "SUCCESS" : "FAILED");
    return recovered ? 0 : 1;
}
if (args is ["--test-startup-and-launch", var testJournal, var launchMarker] && OperatingSystem.IsWindows())
{
    try
    {
        await StartupLaunchSequence.RunAsync(() => RecoverAtStartup(testJournal, waitForOwner: true),
            () => File.WriteAllText(launchMarker, "LAUNCHED"));
        return 0;
    }
    catch { return 1; }
}
if (args.Length == 2 && args[0] == "--test-startup-recovery" && OperatingSystem.IsWindows())
{
    try { await RecoverAtStartup(args[1]); return 0; }
    catch { return 1; }
}
#endif
if (OperatingSystem.IsWindows() &&
    (args is ["--check-install-target", _] ||
     args is [var command] && command is ("--check-install-session" or "--install-logon-task" or "--remove-logon-task")))
{
    var taskCommand = args[0];
    try
    {
        var journal = AppPaths.SessionJournalFile();
        using var owner = DisplaySessionOwner.Acquire(journal);
        if (new JsonSessionJournalStore(journal).Inspect().Status != SessionJournalStatus.Missing)
            throw new InvalidOperationException("Complete session recovery before changing the installation.");
        if (taskCommand == "--check-install-session") return 0;
        using var currentUser = WindowsIdentity.GetCurrent();
        var sid = currentUser.User?.Value ?? throw new InvalidOperationException("Windows user SID unavailable.");
        var executable = Path.Combine(taskCommand == "--check-install-target" ? args[1] : AppContext.BaseDirectory, "CouchPad.Rescue.exe");
        var registration = new RescueLogonTaskRegistration(new WindowsRescueLogonTaskStore());
        if (taskCommand == "--check-install-target")
        {
            try
            {
                _ = registration.IsRegistered(executable, sid);
                new WindowsScheduledStartup().CheckOwnership(Path.Combine(args[1], "CouchPad.exe"));
            }
            catch (InvalidOperationException ex)
            {
                RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), "startup", ex);
                return 3;
            }
            return 0;
        }
        if (taskCommand == "--install-logon-task")
        {
            if (!File.Exists(executable)) throw new FileNotFoundException("Rescue apphost unavailable.", executable);
            registration.Create(executable, sid);
        }
        else
        {
            registration.Remove(executable, sid, preserveOtherInstallations: true);
            new WindowsScheduledStartup().RemoveForUninstall(Path.Combine(AppContext.BaseDirectory, "CouchPad.exe"));
        }
        return 0;
    }
    catch (Exception ex) { RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), "startup", ex); return 1; }
}
if (args is ["--assisted-recovery"] && OperatingSystem.IsWindows())
{
    var recovered = await RunAssistedRecovery(AppPaths.SessionJournalFile());
    try { LaunchApp(recovered ? "--recovery-succeeded" : "--recovery-failed"); }
    catch (Exception ex) { RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), "startup", ex); return 1; }
    return recovered ? 0 : 1;
}
if (args.Length == 1 && args[0] == "--startup-recovery" && OperatingSystem.IsWindows())
{
    try { await RecoverAtStartup(AppPaths.SessionJournalFile()); return 0; }
    catch (Exception ex) { RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), "startup", ex); return 1; }
}
if (args.Length == 1 && args[0] == "--startup-and-launch" && OperatingSystem.IsWindows())
{
    try
    {
        await StartupLaunchSequence.RunAsync(() => RecoverAtStartup(AppPaths.SessionJournalFile(), waitForOwner: true),
            () => LaunchApp("--windows-autostart"));
        return 0;
    }
    catch (Exception ex) { RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), "startup", ex); return 1; }
}
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
        if (journal.RestoreAudioBaseline is not null)
        {
            AudioRecoveryTransaction.ValidateBaseline(journal.RestoreAudioBaseline);
#if !RESCUE_TEST_BACKEND
            WindowsAudioRecoveryBackend.CheckPolicyConfigAvailable();
#endif
        }
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
        // A stuck audio COM call can hold both the decision and mutation locks.
        // On timeout or a broken pipe, terminate only our verified parent before
        // recovery so its process-owned locks are released. Never kill a process
        // for display-only sessions or after an acknowledged decision.
        if (journal.RestoreAudioBaseline is not null && !parentExit.IsCompleted && !parent.HasExited)
        {
            stage = "stop-parent";
            // A normally exiting parent can close its pipe just before the OS
            // reports process termination. Give it a moment to finish.
            await Task.WhenAny(parentExit, Task.Delay(1000));
            if (!parentExit.IsCompleted && !parent.HasExited)
            {
                try
                {
#if RESCUE_TEST_BACKEND
                    if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_PARENT_EXIT_RACE") == "1")
                    {
                        parent.Kill(entireProcessTree: false);
                        await parent.WaitForExitAsync();
                    }
#endif
                    var executable = Path.GetFullPath(parent.MainModule?.FileName ?? throw new InvalidOperationException("Parent executable unavailable."));
#if RESCUE_TEST_BACKEND
                    if (!string.Equals(Path.GetFileName(executable), "RescueProcessTests.exe", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Refusing to terminate an unrecognized test process.");
#else
                    var expectedParent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "CouchPad.exe"));
                    if (!string.Equals(executable, expectedParent, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Refusing to terminate an unrecognized process.");
#endif
                    parent.Kill(entireProcessTree: false);
                    await parent.WaitForExitAsync();
                }
                catch (Exception) when (parent.HasExited)
                {
                    // Natural exit can race MainModule or Kill; its locks are gone.
                }
            }
        }
        stage = "restore";
        await Recover(decisions, transactionId, baselineHash, decisionPath, journal);
        return 0;
    }
    catch (Exception ex) { RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), stage, ex); return 1; }
}

static void LaunchApp(string? argument = null)
{
    var application = Path.Combine(AppContext.BaseDirectory, "CouchPad.exe");
    var info = new ProcessStartInfo(application) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
    if (argument is not null) info.ArgumentList.Add(argument);
    using var started = Process.Start(info) ?? throw new InvalidOperationException("CouchPad could not be started.");
}

static async Task<bool> RunAssistedRecovery(string journalPath)
{
    try
    {
        await RecoverAtStartup(journalPath, waitForOwner: true, assisted: true);
        return true;
    }
    catch (Exception ex)
    {
#if !RESCUE_TEST_BACKEND
        RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), "restore", ex);
#else
        _ = ex;
#endif
        return false;
    }
}

static async Task RecoverAtStartup(string journalPath, bool waitForOwner = false, bool assisted = false)
{
    journalPath = Path.GetFullPath(journalPath);
    using var ownerTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var store = new JsonSessionJournalStore(journalPath);
    DisplaySessionOwner owner;
    bool waited;
    try
    {
        (owner, waited) = waitForOwner
            ? await DisplaySessionOwner.AcquireWhenAvailableAsync(journalPath, ownerTimeout.Token)
            : (DisplaySessionOwner.Acquire(journalPath), false);
    }
    catch (IOException ex) when (!waitForOwner && (ex.HResult & 0xffff) is 32 or 33)
    {
        // Autostart or the App may already own the session. With no journal,
        // this independent task has nothing to do and must not write state.
        if (store.Inspect().Status == SessionJournalStatus.Missing) return;
        throw;
    }
    using var sessionOwner = owner;
    if (waited && !assisted && store.Inspect().Status != SessionJournalStatus.Missing)
        throw new InvalidOperationException("The previous session owner did not clear the recovery journal.");
    var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName + "|" + journalPath.ToUpperInvariant())));
    var lockPath = Path.Combine(Path.GetDirectoryName(journalPath)!, "watchdog-" + identity + ".lock");
    using var watchdogLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var attempts = new StartupRecoveryAttempts(Path.Combine(Path.GetDirectoryName(journalPath)!, "startup-recovery-attempts.json"));
    await StartupRecovery.RunAsync(store, async (entry, cancellationToken) =>
    {
        var id = Guid.NewGuid();
        var hash = RescueDecisionStore.ComputeBaselineSha256(entry);
        if (!assisted) attempts.Begin(hash);
        var path = Path.Combine(Path.GetDirectoryName(journalPath)!, "rescue-decisions", id.ToString("N") + ".json");
        var decisions = new RescueDecisionStore(path);
        await decisions.InitializeAsync(id, hash, cancellationToken);
        if (!await decisions.TryTransitionAsync(id, hash, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested, cancellationToken))
            throw new InvalidDataException("Could not acquire recovery decision.");
        await Restore(entry, path, id, hash);
        if (!await decisions.TryTransitionAsync(id, hash, RescueDecisionState.RecoveryRequested, RescueDecisionState.Recovered, cancellationToken))
            throw new InvalidDataException("Recovery completion could not be recorded.");
    });
    attempts.Clear();
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
    var marker = Environment.GetEnvironmentVariable("COUCHPAD_TEST_MARKER")!;
    await using (var mutation = await DisplayMutationLock.AcquireAsync(transaction.MutationLockPath))
    {
        await transaction.RequireAsync(RescueDecisionState.RecoveryRequested);
        if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_DISPLAY_FAIL") == "1") throw new InvalidOperationException("Injected display recovery failure.");
        await File.WriteAllTextAsync(marker, journal.RestoreAudioBaseline is null ? "RESTORE" : "RESTORE_DISPLAY");
    }
    if (journal.RestoreAudioBaseline is not null)
    {
        var audioGate = Environment.GetEnvironmentVariable("COUCHPAD_TEST_AUDIO_GATE");
        if (!string.IsNullOrEmpty(audioGate))
        {
            await File.WriteAllTextAsync(audioGate + ".waiting", "waiting");
            using var gateTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!File.Exists(audioGate + ".release")) await Task.Delay(25, gateTimeout.Token);
        }
        await GuardedAudioRecovery.RestoreAsync(journal.RestoreAudioBaseline, new FakeAudioRecoveryBackend(), transaction, TimeSpan.FromSeconds(5));
        await File.AppendAllTextAsync(marker, "+AUDIO");
    }
#else
    var display = WindowsDisplayService.ForRecovery(new WindowsDisplayOptions("", 0, 0, AllowWrites: true), transaction);
    await display.RestoreAsync(journal.RestoreDisplay!, CancellationToken.None);
    if (journal.RestoreAudioBaseline is not null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        await GuardedAudioRecovery.RestoreAsync(journal.RestoreAudioBaseline,
            new WindowsAudioRecoveryBackend(allowWrites: true), transaction, TimeSpan.FromSeconds(15));
    }
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

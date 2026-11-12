using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using CouchPad.Platform.Windows;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using System.Text.Json;

// Test-only process: no dependency on, or invocation of, display APIs.
if (!OperatingSystem.IsWindows()) return 2;
var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing apphost path.");
if (args is ["--app-instance-owner", var profile])
{
    using var owner = await WindowsAppInstance.AcquireOrForwardAsync(profile);
    if (owner is null) return 1;
    File.WriteAllText(Path.Combine(profile, "ready"), "");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    while (!File.Exists(Path.Combine(profile, "listen"))) await Task.Delay(25, deadline.Token);
    owner.Listen(token => File.WriteAllTextAsync(Path.Combine(profile, "shown"), "", token));
    while (!File.Exists(Path.Combine(profile, "exit"))) await Task.Delay(25, deadline.Token);
    return 0;
}
if (args is ["--test-app-instance", var testRoot])
{
    var profileRoot = Path.Combine(testRoot, Guid.NewGuid().ToString("N"));
    var info = new ProcessStartInfo(executable) { UseShellExecute = false };
    info.ArgumentList.Add("--app-instance-owner");
    info.ArgumentList.Add(profileRoot);
    using var child = Process.Start(info) ?? throw new InvalidOperationException("App instance test failed to start.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    try
    {
        while (!File.Exists(Path.Combine(profileRoot, "ready"))) await Task.Delay(25, deadline.Token);
        var forwarded = WindowsAppInstance.AcquireOrForwardAsync(profileRoot, deadline.Token);
        await Task.Delay(150, deadline.Token);
        if (forwarded.IsCompleted) throw new InvalidOperationException("Secondary instance did not wait for the listener.");
        File.WriteAllText(Path.Combine(profileRoot, "listen"), "");
        using var secondary = await forwarded;
        if (secondary is not null || !File.Exists(Path.Combine(profileRoot, "shown")))
            throw new InvalidOperationException("Secondary instance did not forward window activation.");
        File.WriteAllText(Path.Combine(profileRoot, "exit"), "");
        await child.WaitForExitAsync(deadline.Token);
        if (child.ExitCode != 0) throw new InvalidOperationException("Primary instance did not exit cleanly.");
        using var successor = await WindowsAppInstance.AcquireOrForwardAsync(profileRoot, deadline.Token);
        if (successor is null) throw new InvalidOperationException("Ownership was not released.");
        Console.WriteLine("APP INSTANCE: delayed listener, cross-process forwarding with ACK, and ownership transfer passed.");
        return 0;
    }
    finally
    {
        if (!child.HasExited) child.Kill();
    }
}
var childExecutable = Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") ?? executable;
var marker = Environment.GetEnvironmentVariable("COUCHPAD_TEST_MARKER") ?? throw new InvalidOperationException("Missing isolated test marker.");
if (args.Length == 1 && args[0] == "--audio-victim")
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}
if (args.Length == 8 && args[0].StartsWith("CouchPad-rescue-", StringComparison.Ordinal))
{
    var transactionId = Guid.Parse(args[5]);
    var decisions = new RescueDecisionStore(args[7]);
    if ((await decisions.ReadAsync(transactionId, args[6]))?.State != RescueDecisionState.Armed) return 4;
    using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await pipe.ConnectAsync(deadline.Token);
    using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, true);
    var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
    if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_READY") == "1") return 18;
    using var parent = Process.GetProcessById(int.Parse(args[2]));
    var dead = parent.WaitForExitAsync();
    await writer.WriteLineAsync("READY");
    var input = reader.ReadLineAsync();
    var timeout = Task.Delay(int.Parse(args[4]));
    await Task.WhenAny(input, dead, timeout);
    if (!dead.IsCompleted && !timeout.IsCompleted && input.IsCompletedSuccessfully && input.Result is "COMMIT" or "ABORT")
    {
        if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_DECISION") == "1") return 19;
        var state = input.Result == "COMMIT" ? RescueDecisionState.Committed : RescueDecisionState.Aborted;
        if (!await decisions.TryTransitionAsync(transactionId, args[6], RescueDecisionState.Armed, state)) return 5;
        await File.WriteAllTextAsync(marker, input.Result);
        if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_DROP_ACK") == "1") return 0;
        await writer.WriteLineAsync("ACK");
    }
    else
    {
        if (!await decisions.TryTransitionAsync(transactionId, args[6], RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested)) return 5;
        await File.WriteAllTextAsync(marker, "RESTORE");
        if (!await decisions.TryTransitionAsync(transactionId, args[6], RescueDecisionState.RecoveryRequested, RescueDecisionState.Recovered)) return 5;
    }
    return 0;
}
if (args.Length == 1 && args[0] is "--parent-die-before" or "--parent-die-after")
{
    var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(marker + ".journal"))!;
    var transaction = new RescueTransactionContext(stateDirectory);
    var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(8), allowRecovery: true, transaction: transaction);
    _ = await guard.BeginAsync("test");
    await using var mutation = await DisplayMutationLock.AcquireAsync(transaction.MutationLockPath);
    if (args[0] == "--parent-die-after") await File.WriteAllTextAsync(marker + ".applied", "fake apply");
    await File.WriteAllTextAsync(marker + ".ready", "ready");
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}
if (args.Length == 1 && args[0] is "--audio-parent-die" or "--audio-parent-timeout")
{
    var journalPath = marker + ".journal";
    var journal = new JsonSessionJournalStore(journalPath).Load() ?? throw new InvalidDataException("Missing fake audio journal.");
    var id = Guid.NewGuid();
    var hash = RescueDecisionStore.ComputeBaselineSha256(journal);
    var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(journalPath))!;
    var decisionPath = Path.Combine(stateDirectory, "rescue-decisions", id.ToString("N") + ".json");
    await new RescueDecisionStore(decisionPath).InitializeAsync(id, hash);
    var audioPipeName = "CouchPad-rescue-" + Guid.NewGuid().ToString("N");
    using var server = new NamedPipeServerStream(audioPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    using var self = Process.GetCurrentProcess();
    var info = new ProcessStartInfo(childExecutable) { UseShellExecute = false, CreateNoWindow = true };
    foreach (var argument in new[] { audioPipeName, journalPath, self.Id.ToString(), self.StartTime.ToUniversalTime().Ticks.ToString(), "5000", id.ToString("D"), hash, decisionPath })
        info.ArgumentList.Add(argument);
    using var child = Process.Start(info) ?? throw new InvalidOperationException("Audio rescue failed to start.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await server.WaitForConnectionAsync(deadline.Token);
    using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true);
    if (await reader.ReadLineAsync(deadline.Token) != "READY") throw new InvalidOperationException("Audio rescue was not ready.");
    await using var mutation = await DisplayMutationLock.AcquireAsync(Path.Combine(stateDirectory, "display-mutation.lock"));
    await new RescueDecisionStore(decisionPath).ExecuteIfStateAsync(id, hash, RescueDecisionState.Armed, () =>
    {
        File.WriteAllText(marker + ".ready", decisionPath + "|" + id.ToString("D") + "|" + hash);
        Thread.Sleep(Timeout.Infinite);
    });
    return 0;
}
if (args.Length != 0) return 2;
var path = new byte[72]; var modes = new byte[128];
BitConverter.GetBytes(1u).CopyTo(path, 0); BitConverter.GetBytes(7u).CopyTo(path, 8);
BitConverter.GetBytes(1u).CopyTo(path, 20); BitConverter.GetBytes(10u).CopyTo(path, 28);
BitConverter.GetBytes(1u).CopyTo(path, 32); BitConverter.GetBytes(1u).CopyTo(path, 68);
BitConverter.GetBytes(1u).CopyTo(modes, 0); BitConverter.GetBytes(7u).CopyTo(modes, 4); BitConverter.GetBytes(1u).CopyTo(modes, 8);
BitConverter.GetBytes(2u).CopyTo(modes, 64); BitConverter.GetBytes(10u).CopyTo(modes, 68); BitConverter.GetBytes(1u).CopyTo(modes, 72);
var snapshot = new WindowsDisplayService.Snapshot(1, 2, 72, 64, Convert.ToBase64String(path), Convert.ToBase64String(modes), []);
var entry = new SessionJournalEntry(SessionJournalEntry.PendingState, DisplayMode.TvOnly, CouchPad.Core.Audio.AudioMode.KeepCurrent, DateTimeOffset.UtcNow)
{
    RestoreDisplay = new DisplayState(DisplayMode.InternalOnly, true, false) { Topology = JsonSerializer.Serialize(snapshot) }
};
new JsonSessionJournalStore(marker + ".journal").Save(entry);

async Task<(RescueDecisionStore Store, Guid Id, string Hash)> AddDecisionArguments(ProcessStartInfo info, SessionJournalEntry? journalEntry = null)
{
    var id = Guid.NewGuid();
    var hash = RescueDecisionStore.ComputeBaselineSha256(journalEntry ?? entry);
    var decisionPath = Path.Combine(Path.GetDirectoryName(marker + ".journal")!, "rescue-decisions", id.ToString("N") + ".json");
    var store = new RescueDecisionStore(decisionPath);
    await store.InitializeAsync(id, hash);
    info.ArgumentList.Add(id.ToString("D"));
    info.ArgumentList.Add(hash);
    info.ArgumentList.Add(decisionPath);
    return (store, id, hash);
}

async Task<string> WaitMarker(string file, TimeSpan limit)
{
    var until = Stopwatch.StartNew();
    while (until.Elapsed < limit)
    {
        try { if (File.Exists(file)) return await File.ReadAllTextAsync(file); }
        catch (IOException) { }
        await Task.Delay(50);
    }
    throw new TimeoutException("Test marker not received: " + file);
}
void Reset() { if (File.Exists(marker)) File.Delete(marker); if (File.Exists(marker + ".ready")) File.Delete(marker + ".ready"); if (File.Exists(marker + ".applied")) File.Delete(marker + ".applied"); }
foreach (var mode in new[] { "COMMIT", "ABORT", "ABANDON", "TIMEOUT", "DISPOSE" })
{
    Reset();
    var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(5), allowRecovery: true);
    await using (var lease = await guard.BeginAsync("test"))
    {
        if (mode == "COMMIT") await lease.CommitAsync();
        if (mode == "ABORT") await lease.AbortAsync();
        if (mode is "COMMIT" or "ABORT")
        {
            await Task.Delay(250);
            if (lease.FailureToken.IsCancellationRequested) throw new InvalidOperationException("Normal release reported watchdog failure.");
        }
        if (mode == "ABANDON") lease.Abandon();
        if (mode == "DISPOSE")
        {
            await lease.DisposeAsync();
            await Task.Delay(250);
            if (File.Exists(marker)) throw new InvalidOperationException("Dispose caused an immediate signal.");
        }
        var observed = await WaitMarker(marker, TimeSpan.FromSeconds(10));
        if (observed != (mode is "COMMIT" or "ABORT" ? mode : "RESTORE")) throw new InvalidOperationException(mode + ": " + observed);
    }
    Console.WriteLine(mode + ": PASS");
}
foreach (var signal in new[] { "COMMIT", "ABORT" })
{
    Reset();
    Environment.SetEnvironmentVariable("COUCHPAD_TEST_DROP_ACK", "1");
    try
    {
        var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(8), allowRecovery: true);
        await using var lease = await guard.BeginAsync("test-dropped-ack");
        if (signal == "COMMIT") await lease.CommitAsync(); else await lease.AbortAsync();
        if (await WaitMarker(marker, TimeSpan.FromSeconds(5)) != signal) throw new InvalidOperationException("Terminal decision was not preserved.");
        await Task.Delay(250);
        Console.WriteLine("LOST_" + signal + "_ACK_RECONCILED: PASS");
    }
    finally { Environment.SetEnvironmentVariable("COUCHPAD_TEST_DROP_ACK", null); }
}
Reset();
Environment.SetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_DECISION", "1");
try
{
    var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(8), allowRecovery: true);
    await using var lease = await guard.BeginAsync("test-uncertain-decision");
    try { await lease.CommitAsync(); throw new InvalidOperationException("Unconfirmed decision was accepted."); }
    catch (RescueDecisionUncertainException) { }
    Console.WriteLine("DEATH_BEFORE_DECISION_UNCERTAIN: PASS");
}
finally { Environment.SetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_DECISION", null); }
Reset();
Environment.SetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_READY", "1");
try
{
    var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(8), allowRecovery: true);
    try { _ = await guard.BeginAsync("test-startup-exit"); throw new InvalidOperationException("Uncertain startup was accepted."); }
    catch (RescueDecisionUncertainException) { }
    Console.WriteLine("EXIT_BEFORE_READY_UNCERTAIN: PASS");
}
finally { Environment.SetEnvironmentVariable("COUCHPAD_TEST_EXIT_BEFORE_READY", null); }
if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") is not null)
{
    Reset();
    var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(marker + ".journal"))!;
    var transaction = new RescueTransactionContext(stateDirectory);
    var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(5),
        allowRecovery: true, transaction: transaction);
    await using var lease = await guard.BeginAsync("test-timeout-during-apply");
    await using (var held = await DisplayMutationLock.AcquireAsync(transaction.MutationLockPath))
    {
        var timer = Stopwatch.StartNew();
        var recoveryRequested = false;
        while (timer.Elapsed < TimeSpan.FromSeconds(9))
        {
            try { await transaction.RequireAsync(RescueDecisionState.Armed); }
            catch (RescueDecisionUncertainException) { recoveryRequested = true; break; }
            await Task.Delay(50);
        }
        if (!recoveryRequested) throw new TimeoutException("Recovery was not requested.");
        if (File.Exists(marker)) throw new InvalidOperationException("Restore ran concurrently with the simulated apply.");
    }
    if (await WaitMarker(marker, TimeSpan.FromSeconds(10)) != "RESTORE") throw new InvalidOperationException("Restore did not run after the lock was released.");
    using var waitForExit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    using var observed = CancellationTokenSource.CreateLinkedTokenSource(lease.FailureToken, waitForExit.Token);
    try { await Task.Delay(Timeout.InfiniteTimeSpan, observed.Token); } catch (OperationCanceledException) { }
    if (!lease.FailureToken.IsCancellationRequested) throw new TimeoutException("Watchdog did not exit after recovery.");
    Console.WriteLine("TIMEOUT_WAITS_FOR_APPLY_LOCK: PASS");
}
if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") is not null)
{
    Reset();
    var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(marker + ".journal"))!;
    var transaction = new RescueTransactionContext(stateDirectory);
    var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(5),
        allowRecovery: true, transaction: transaction);
    await using var lease = await guard.BeginAsync("test-timeout-during-decision-lock");
    await using (var held = await DisplayMutationLock.AcquireAsync(transaction.MutationLockPath))
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var apply = Task.Run(async () => await transaction.ExecuteIfStateAsync(RescueDecisionState.Armed, () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(12))) throw new TimeoutException("Fake apply gate timed out.");
        }));
        if (!entered.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Fake apply did not enter decision lock.");
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6));
            if (File.Exists(marker)) throw new InvalidOperationException("Restore ran while fake apply held the decision lock.");
        }
        finally { release.Set(); await apply; }
    }
    if (await WaitMarker(marker, TimeSpan.FromSeconds(10)) != "RESTORE") throw new InvalidOperationException("Restore did not run after the decision lock was released.");
    Console.WriteLine("TIMEOUT_WAITS_FOR_DECISION_LOCK: PASS");
}
if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") is not null)
{
    Reset();
    Environment.SetEnvironmentVariable("COUCHPAD_TEST_EXIT_AFTER_READY", "1");
    try
    {
        var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(8), allowRecovery: true);
        await using var lease = await guard.BeginAsync("test-watchdog-exit");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var observed = CancellationTokenSource.CreateLinkedTokenSource(lease.FailureToken, deadline.Token);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, observed.Token); } catch (OperationCanceledException) { }
        if (!lease.FailureToken.IsCancellationRequested) throw new InvalidOperationException("Watchdog exit was not signaled.");
        try { lease.ThrowIfUnavailable(); throw new InvalidOperationException("Watchdog was reported available."); }
        catch (InvalidOperationException ex) when (ex.Message == "Rescue watchdog unavailable.") { }
        Console.WriteLine("WATCHDOG_EXIT_AFTER_READY: PASS");
    }
    finally { Environment.SetEnvironmentVariable("COUCHPAD_TEST_EXIT_AFTER_READY", null); }
}
if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") is not null)
{
    Reset();
    var ackGate = marker + ".ack";
    Environment.SetEnvironmentVariable("COUCHPAD_TEST_ACK_GATE", ackGate);
    try
    {
        var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(8), allowRecovery: true);
        await using var lease = await guard.BeginAsync("test-cancel-ack");
        using var cancel = new CancellationTokenSource();
        var commit = lease.CommitAsync(cancel.Token);
        await WaitMarker(ackGate + ".waiting", TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await File.WriteAllTextAsync(ackGate + ".release", "release");
        await commit;
        if (await WaitMarker(marker, TimeSpan.FromSeconds(5)) != "COMMIT") throw new InvalidOperationException("Commit acknowledgement failed.");
        Console.WriteLine("ACK_CANCELLATION_AFTER_SEND: PASS");
    }
    finally
    {
        Environment.SetEnvironmentVariable("COUCHPAD_TEST_ACK_GATE", null);
        if (File.Exists(ackGate + ".waiting")) File.Delete(ackGate + ".waiting");
        if (File.Exists(ackGate + ".release")) File.Delete(ackGate + ".release");
    }
}
if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") is not null)
{
    Reset();
    var guard = new WindowsRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(8), allowRecovery: true);
    await using var first = await guard.BeginAsync("test-lock");
    using var currentForLock = Process.GetCurrentProcess();
    var info = new ProcessStartInfo(childExecutable) { UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add("CouchPad-rescue-" + Guid.NewGuid().ToString("N")); info.ArgumentList.Add(marker + ".journal");
    info.ArgumentList.Add(currentForLock.Id.ToString()); info.ArgumentList.Add(currentForLock.StartTime.ToUniversalTime().Ticks.ToString()); info.ArgumentList.Add("5000");
    await AddDecisionArguments(info);
    using var duplicate = Process.Start(info) ?? throw new InvalidOperationException("Duplicate process failed to start.");
    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await duplicate.WaitForExitAsync(limit.Token);
    if (duplicate.ExitCode != 3) throw new InvalidOperationException("Duplicate watchdog was not refused: " + duplicate.ExitCode);
    await first.AbortAsync();
    Console.WriteLine("SINGLE_INSTANCE: PASS");
}
if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") is not null)
{
    Reset();
    await Task.Delay(200);
    var ackGate = marker + ".broken";
    var brokenName = "CouchPad-rescue-" + Guid.NewGuid().ToString("N");
    using var server = new NamedPipeServerStream(brokenName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    using var current = Process.GetCurrentProcess();
    var info = new ProcessStartInfo(childExecutable) { UseShellExecute = false, CreateNoWindow = true };
    info.Environment["COUCHPAD_TEST_ACK_GATE"] = ackGate;
    info.ArgumentList.Add(brokenName); info.ArgumentList.Add(marker + ".journal"); info.ArgumentList.Add(current.Id.ToString());
    info.ArgumentList.Add(current.StartTime.ToUniversalTime().Ticks.ToString()); info.ArgumentList.Add("5000");
    var decision = await AddDecisionArguments(info);
    using var child = Process.Start(info) ?? throw new InvalidOperationException("ACK test child failed to start.");
    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await server.WaitForConnectionAsync(limit.Token);
    var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true);
    var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
    if (await reader.ReadLineAsync(limit.Token) != "READY") throw new InvalidOperationException("Missing READY.");
    await writer.WriteLineAsync("COMMIT");
    await WaitMarker(ackGate + ".waiting", TimeSpan.FromSeconds(5));
    server.Disconnect();
    await File.WriteAllTextAsync(ackGate + ".release", "release");
    await child.WaitForExitAsync(limit.Token);
    if (child.ExitCode != 0 || await File.ReadAllTextAsync(marker) != "COMMIT" ||
        (await decision.Store.ReadAsync(decision.Id, decision.Hash))?.State != RescueDecisionState.Committed)
        throw new InvalidOperationException("Committed decision was not retained after ACK loss.");
    if (File.Exists(ackGate + ".waiting")) File.Delete(ackGate + ".waiting");
    if (File.Exists(ackGate + ".release")) File.Delete(ackGate + ".release");
    Console.WriteLine("ACK_PIPE_BREAK_AFTER_COMMIT: PASS");
}
Reset();
await Task.Delay(200);
var pipeName = "CouchPad-rescue-" + Guid.NewGuid().ToString("N");
using (var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
using (var current = Process.GetCurrentProcess())
{
    var info = new ProcessStartInfo(childExecutable) { UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add(pipeName); info.ArgumentList.Add(marker + ".journal");
    info.ArgumentList.Add(current.Id.ToString()); info.ArgumentList.Add(current.StartTime.ToUniversalTime().Ticks.ToString()); info.ArgumentList.Add("5000");
    var decision = await AddDecisionArguments(info);
    using var child = Process.Start(info) ?? throw new InvalidOperationException("Pipe-break child failed to start.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await server.WaitForConnectionAsync(deadline.Token);
    using (var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true))
        if (await reader.ReadLineAsync(deadline.Token) != "READY") throw new InvalidOperationException("Missing READY.");
    server.Disconnect();
    if (await WaitMarker(marker, TimeSpan.FromSeconds(10)) != "RESTORE") throw new InvalidOperationException("Pipe break did not restore.");
    await child.WaitForExitAsync(deadline.Token);
    if (child.ExitCode != 0) throw new InvalidOperationException("Pipe-break child failed.");
    if ((await decision.Store.ReadAsync(decision.Id, decision.Hash))?.State != RescueDecisionState.Recovered) throw new InvalidOperationException("Pipe-break recovery decision missing.");
    Console.WriteLine("PIPE_BREAK: PASS");
}
if (Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") is not null)
{
    Reset();
    var guardAudioEntry = entry with { RestoreAudioBaseline = new CouchPad.Core.Audio.AudioBaseline(1, "a", "b", "a", [new("a", 0.5f, false), new("b", 0.2f, true)]) };
    new JsonSessionJournalStore(marker + ".journal").Save(guardAudioEntry);
    var audioGuard = new WindowsAudioRescueGuard(childExecutable, marker + ".journal", TimeSpan.FromSeconds(5),
        new RescueTransactionContext(Path.GetDirectoryName(Path.GetFullPath(marker + ".journal"))!), allowRecovery: true);
    await using (var lease = await audioGuard.BeginAsync("experimental audio test"))
    {
        await lease.AbortAsync();
        if (await WaitMarker(marker, TimeSpan.FromSeconds(5)) != "ABORT") throw new InvalidOperationException("Audio guard abort failed.");
    }
    Console.WriteLine("AUDIO_GUARD_READY_ABORT: PASS");
    foreach (var scenario in new[] { "success", "failure", "lock", "exit-race" })
    {
        var fail = scenario == "failure";
        Reset();
        var audioEntry = entry with { RestoreAudioBaseline = new CouchPad.Core.Audio.AudioBaseline(1, "a", "b", "a", [new("a", 0.5f, false), new("b", 0.2f, true)]) };
        new JsonSessionJournalStore(marker + ".journal").Save(audioEntry);
        var testPipe = "CouchPad-rescue-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(testPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var victimInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        victimInfo.ArgumentList.Add("--audio-victim");
        using var victim = Process.Start(victimInfo) ?? throw new InvalidOperationException("Audio test parent failed to start.");
        var info = new ProcessStartInfo(childExecutable) { UseShellExecute = false, CreateNoWindow = true };
        if (fail) info.Environment["COUCHPAD_TEST_AUDIO_FAIL"] = "1";
        if (scenario == "exit-race") info.Environment["COUCHPAD_TEST_PARENT_EXIT_RACE"] = "1";
        var audioGate = marker + ".audio-gate";
        if (scenario == "lock") info.Environment["COUCHPAD_TEST_AUDIO_GATE"] = audioGate;
        info.ArgumentList.Add(testPipe); info.ArgumentList.Add(marker + ".journal");
        info.ArgumentList.Add(victim.Id.ToString()); info.ArgumentList.Add(victim.StartTime.ToUniversalTime().Ticks.ToString()); info.ArgumentList.Add("5000");
        var decision = await AddDecisionArguments(info, audioEntry);
        using var child = Process.Start(info) ?? throw new InvalidOperationException("Audio recovery test failed to start.");
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await server.WaitForConnectionAsync(limit.Token);
        using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true);
        if (await reader.ReadLineAsync(limit.Token) != "READY") throw new InvalidOperationException("Missing audio recovery READY.");
        server.Disconnect();
        if (scenario == "lock")
        {
            await WaitMarker(audioGate + ".waiting", TimeSpan.FromSeconds(5));
            var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(marker + ".journal"))!;
            await using (var held = await DisplayMutationLock.AcquireAsync(Path.Combine(stateDirectory, "display-mutation.lock")))
            {
                await File.WriteAllTextAsync(audioGate + ".release", "release");
                await Task.Delay(300);
                if ((await File.ReadAllTextAsync(marker)) != "RESTORE_DISPLAY" || child.HasExited)
                    throw new InvalidOperationException("Audio recovery did not wait for the mutation lock.");
            }
        }
        await child.WaitForExitAsync(limit.Token);
        if (!victim.HasExited) { victim.Kill(entireProcessTree: false); await victim.WaitForExitAsync(); }
        var observed = await WaitMarker(marker, TimeSpan.FromSeconds(5));
        var state = (await decision.Store.ReadAsync(decision.Id, decision.Hash))?.State;
        if (fail ? child.ExitCode == 0 || observed != "RESTORE_DISPLAY" || state != RescueDecisionState.RecoveryRequested
                 : child.ExitCode != 0 || observed != "RESTORE_DISPLAY+AUDIO" || state != RescueDecisionState.Recovered)
            throw new InvalidOperationException("Unexpected audio recovery outcome.");
        Console.WriteLine(fail ? "AUDIO_FAILURE_REMAINS_PENDING: PASS" : scenario == "lock" ? "AUDIO_WAITS_FOR_MUTATION_LOCK: PASS" : scenario == "exit-race" ? "AUDIO_PARENT_EXIT_RACE: PASS" : "AUDIO_RECOVERY_FAKE: PASS");
    }
    foreach (var scenario in new[] { "die", "timeout" })
    {
        Reset();
        var crashAudioEntry = entry with { RestoreAudioBaseline = new CouchPad.Core.Audio.AudioBaseline(1, "a", "b", "a", [new("a", 0.5f, false), new("b", 0.2f, true)]) };
        new JsonSessionJournalStore(marker + ".journal").Save(crashAudioEntry);
        var crashInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        crashInfo.ArgumentList.Add("--audio-parent-" + scenario);
        using var parent = Process.Start(crashInfo) ?? throw new InvalidOperationException("Audio parent failed to start.");
        try
        {
            var decisionDetails = (await WaitMarker(marker + ".ready", TimeSpan.FromSeconds(10))).Split('|');
            if (scenario == "die") parent.Kill(entireProcessTree: false);
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(10))
            {
                try { if (File.Exists(marker) && await File.ReadAllTextAsync(marker) == "RESTORE_DISPLAY+AUDIO") break; }
                catch (IOException) { }
                await Task.Delay(50);
            }
            if (!File.Exists(marker) || await File.ReadAllTextAsync(marker) != "RESTORE_DISPLAY+AUDIO")
                throw new InvalidOperationException("Audio was not restored after parent death.");
            var decisions = new RescueDecisionStore(decisionDetails[0]);
            var decisionId = Guid.Parse(decisionDetails[1]);
            while (timer.Elapsed < TimeSpan.FromSeconds(10) &&
                   (await decisions.ReadAsync(decisionId, decisionDetails[2]))?.State != RescueDecisionState.Recovered)
                await Task.Delay(50);
            if ((await decisions.ReadAsync(decisionId, decisionDetails[2]))?.State != RescueDecisionState.Recovered)
                throw new InvalidOperationException("Audio recovery was not recorded after parent death.");
            Console.WriteLine(scenario == "die" ? "AUDIO_PARENT_DEATH: PASS" : "AUDIO_TIMEOUT_RELEASES_PARENT_LOCK: PASS");
        }
        finally { if (!parent.HasExited) { parent.Kill(entireProcessTree: false); await parent.WaitForExitAsync(); } }
    }
    new JsonSessionJournalStore(marker + ".journal").Save(entry);
}
foreach (var stage in new[] { "before", "after" })
{
    Reset();
    var parentInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
    parentInfo.ArgumentList.Add("--parent-die-" + stage);
    using var parent = Process.Start(parentInfo) ?? throw new InvalidOperationException("Parent harness failed to start.");
    try
    {
        await WaitMarker(marker + ".ready", TimeSpan.FromSeconds(10));
        if (stage == "after" && !File.Exists(marker + ".applied")) throw new InvalidOperationException("Fake apply was not recorded.");
        parent.Kill(entireProcessTree: false);
        await parent.WaitForExitAsync();
        if (await WaitMarker(marker, TimeSpan.FromSeconds(10)) != "RESTORE") throw new InvalidOperationException("Parent death did not restore.");
        Console.WriteLine("PARENT_DEATH_" + stage.ToUpperInvariant() + ": PASS");
    }
    finally { if (!parent.HasExited) { parent.Kill(entireProcessTree: false); await parent.WaitForExitAsync(); } }
}

Reset();
return 0;



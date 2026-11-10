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
var childExecutable = Environment.GetEnvironmentVariable("COUCHPAD_TEST_CHILD") ?? executable;
var marker = Environment.GetEnvironmentVariable("COUCHPAD_TEST_MARKER") ?? throw new InvalidOperationException("Missing isolated test marker.");
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

async Task<(RescueDecisionStore Store, Guid Id, string Hash)> AddDecisionArguments(ProcessStartInfo info)
{
    var id = Guid.NewGuid();
    var hash = RescueDecisionStore.ComputeBaselineSha256(entry);
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



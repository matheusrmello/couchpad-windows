using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CouchPad.Core.Automation;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (args.Length != 5 || !OperatingSystem.IsWindows()) return 2;
var pipeName = args[0];
var journalPath = Path.GetFullPath(args[1]);
if (!int.TryParse(args[2], out var parentId) || !long.TryParse(args[3], out var startedTicks) ||
    !int.TryParse(args[4], out var timeoutMs) || timeoutMs < 5000 || timeoutMs > 600000) return 2;
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
    if (journal is null || (journal.State != SessionJournalEntry.PendingState && journal.State != SessionJournalEntry.ActiveState) || journal.RestoreDisplay?.Topology is null) throw new InvalidDataException("Recovery journal is unavailable.");
    var snapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(journal.RestoreDisplay.Topology);
    if (snapshot is null || !DisplaySnapshotComparer.SemanticallyEqual(snapshot, snapshot)) throw new InvalidDataException("Recovery topology is invalid.");
    Process? parent;
    try { parent = Process.GetProcessById(parentId); }
    catch (ArgumentException) { stage = "restore"; await Restore(journal); return 0; }
    using var parentHandle = parent;
    if (parent.StartTime.ToUniversalTime().Ticks != startedTicks) { stage = "restore"; await Restore(journal); return 0; }
    stage = "connect";
    using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await pipe.ConnectAsync(connectTimeout.Token);
    using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
    var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
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
        await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("COUCHPAD_TEST_MARKER")!, signal.Result!);
        var ackGate = Environment.GetEnvironmentVariable("COUCHPAD_TEST_ACK_GATE");
        if (!string.IsNullOrEmpty(ackGate))
        {
            await File.WriteAllTextAsync(ackGate + ".waiting", "waiting");
            using var gateTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!File.Exists(ackGate + ".release")) await Task.Delay(25, gateTimeout.Token);
        }
#endif
        try { await writer.WriteLineAsync("ACK"); }
        catch (IOException)
        {
            stage = "restore";
            await Restore(journal);
            return 0;
        }
        return 0;
    }
    stage = "restore";
    await Restore(journal);
    return 0;
}
catch (Exception ex) { RescueFailureLog.TryWrite(AppPaths.LogsDirectory(), stage, ex); return 1; }
}
static async Task Restore(SessionJournalEntry journal)
{
#if RESCUE_TEST_BACKEND
    await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("COUCHPAD_TEST_MARKER")!, "RESTORE");
#else
    var display = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0, AllowWrites: true));
    await display.RestoreAsync(journal.RestoreDisplay!, CancellationToken.None);
#endif
}


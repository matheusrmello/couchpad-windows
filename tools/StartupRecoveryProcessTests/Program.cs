using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows() || args.Length != 1) return 2;
var child = Path.GetFullPath(args[0]);
var root = Path.Combine(Path.GetTempPath(), "CouchPadStartupTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
foreach (var state in new[] { SessionJournalEntry.PendingState, SessionJournalEntry.ActiveState })
{
    var dir = Path.Combine(root, state);
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    var marker = Path.Combine(dir, "marker");
    new JsonSessionJournalStore(path).Save(Entry(state));
    if (await Run(child, path, marker, false) != 0 || File.Exists(path) || File.ReadAllText(marker) != "RESTORE_DISPLAY+AUDIO")
        throw new InvalidOperationException($"Recovery failed for {state}.");
    Console.WriteLine($"{state}: recovered and journal cleared");
}
{
    var dir = Path.Combine(root, "missing");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    var marker = Path.Combine(dir, "marker");
    if (await Run(child, path, marker, false) != 0 || File.Exists(path) || File.Exists(marker))
        throw new InvalidOperationException("Missing journal must be a no-op.");
    Console.WriteLine("missing journal: no recovery attempted");
}
{
    var dir = Path.Combine(root, "malformed");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    var marker = Path.Combine(dir, "marker");
    await File.WriteAllTextAsync(path, "{ malformed");
    if (await Run(child, path, marker, false) == 0 || !File.Exists(path) || File.Exists(marker))
        throw new InvalidOperationException("Malformed journal must block recovery.");
    Console.WriteLine("malformed journal: no recovery attempted, journal preserved");
}
{
    var dir = Path.Combine(root, "watchdog-locked");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    var marker = Path.Combine(dir, "marker");
    new JsonSessionJournalStore(path).Save(Entry(SessionJournalEntry.PendingState));
    var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName + "|" + path.ToUpperInvariant())));
    using (var held = new FileStream(Path.Combine(dir, "watchdog-" + identity + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
    {
        if (await Run(child, path, marker, false) == 0 || !File.Exists(path) || File.Exists(marker))
            throw new InvalidOperationException("Watchdog ownership must block startup recovery.");
    }
    Console.WriteLine("watchdog lock: competing recovery blocked");
}
{
    var dir = Path.Combine(root, "failure");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    var marker = Path.Combine(dir, "marker");
    new JsonSessionJournalStore(path).Save(Entry(SessionJournalEntry.ActiveState));
    if (await Run(child, path, marker, true) == 0 || !File.Exists(path))
        throw new InvalidOperationException("Recovery failure did not preserve journal.");
    Console.WriteLine("injected audio failure: journal preserved");
}
{
    var dir = Path.Combine(root, "display-failure");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    var marker = Path.Combine(dir, "marker");
    new JsonSessionJournalStore(path).Save(Entry(SessionJournalEntry.PendingState));
    var info = ChildInfo(child, path, marker);
    info.Environment["COUCHPAD_TEST_DISPLAY_FAIL"] = "1";
    using (var process = Process.Start(info) ?? throw new InvalidOperationException("Fake rescue failed to start."))
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode == 0 || !File.Exists(path) || File.Exists(marker))
            throw new InvalidOperationException("Display failure did not block audio and preserve journal.");
    }
    if (await Run(child, path, marker, false) != 0 || File.Exists(path) || File.ReadAllText(marker) != "RESTORE_DISPLAY+AUDIO")
        throw new InvalidOperationException("Retry after display failure failed.");
    Console.WriteLine("injected display failure: audio blocked, journal preserved and retry succeeded");
}
{
    var dir = Path.Combine(root, "retry-limit");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    new JsonSessionJournalStore(path).Save(Entry(SessionJournalEntry.ActiveState));
    for (var i = 0; i < 3; i++)
        if (await Run(child, path, Path.Combine(dir, "failure-" + i), true) == 0 || !File.Exists(path))
            throw new InvalidOperationException("Injected failure did not preserve journal.");
    var marker = Path.Combine(dir, "blocked");
    if (await Run(child, path, marker, false) == 0 || !File.Exists(path) || File.Exists(marker))
        throw new InvalidOperationException("Retry limit failed to block recovery.");
    Console.WriteLine("retry limit: fourth automatic attempt blocked, journal preserved");
}
{
    var dir = Path.Combine(root, "interrupted");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "session.json");
    var marker = Path.Combine(dir, "marker");
    var gate = Path.Combine(dir, "gate");
    new JsonSessionJournalStore(path).Save(Entry(SessionJournalEntry.PendingState));
    var info = ChildInfo(child, path, marker);
    info.Environment["COUCHPAD_TEST_AUDIO_GATE"] = gate;
    using var process = Process.Start(info) ?? throw new InvalidOperationException("Fake rescue failed to start.");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    while (!File.Exists(gate + ".waiting"))
    {
        if (process.HasExited) throw new InvalidOperationException("Rescue exited before the controlled interruption.");
        await Task.Delay(25, timeout.Token);
    }
    if (await Run(child, path, Path.Combine(dir, "competitor"), false) == 0 || !File.Exists(path))
        throw new InvalidOperationException("Concurrent recovery was not blocked.");
    process.Kill(entireProcessTree: false);
    await process.WaitForExitAsync(timeout.Token);
    if (!File.Exists(path)) throw new InvalidOperationException("Interrupted rescue lost its journal.");
    if (await Run(child, path, marker, false) != 0 || File.Exists(path) || File.ReadAllText(marker) != "RESTORE_DISPLAY+AUDIO")
        throw new InvalidOperationException("Retry after interruption failed.");
    Console.WriteLine("concurrent recovery blocked; interruption preserved journal and retry succeeded");
}
return 0;

static async Task<int> Run(string child, string journal, string marker, bool failAudio)
{
    var info = ChildInfo(child, journal, marker);
    if (failAudio) info.Environment["COUCHPAD_TEST_AUDIO_FAIL"] = "1";
    using var process = Process.Start(info) ?? throw new InvalidOperationException("Fake rescue failed to start.");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await process.WaitForExitAsync(timeout.Token);
    return process.ExitCode;
}

static ProcessStartInfo ChildInfo(string child, string journal, string marker)
{
    var info = new ProcessStartInfo(child) { UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add("--test-startup-recovery");
    info.ArgumentList.Add(journal);
    info.Environment["COUCHPAD_TEST_MARKER"] = marker;
    return info;
}

static SessionJournalEntry Entry(string state)
{
    var path = new byte[72];
    BitConverter.GetBytes(1u).CopyTo(path, 32);
    BitConverter.GetBytes(1u).CopyTo(path, 68);
    var modes = new byte[128];
    BitConverter.GetBytes(1u).CopyTo(modes, 0);
    BitConverter.GetBytes(2u).CopyTo(modes, 64);
    var snapshot = new WindowsDisplayService.Snapshot(1, 2, 72, 64, Convert.ToBase64String(path), Convert.ToBase64String(modes), []);
    return new(state, DisplayMode.TvOnly, AudioMode.MoveToHdmi, DateTimeOffset.UtcNow)
    {
        RestoreDisplay = new(DisplayMode.InternalOnly, true, false) { Topology = JsonSerializer.Serialize(snapshot) },
        RestoreAudioBaseline = new AudioBaseline(1, "monitor", "monitor", "monitor", [new("monitor", .5f, false)]),
    };
}

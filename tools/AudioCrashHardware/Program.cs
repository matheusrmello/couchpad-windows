using System.Diagnostics;
using System.Text.Json;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows() || args.Length != 2) return 2;
var rescuePath = Path.GetFullPath(args[1]);
var journalPath = AppPaths.SessionJournalFile();
var journal = new JsonSessionJournalStore(journalPath);
if (args[0] == "--child")
{
    using var owner = DisplaySessionOwner.Acquire(journalPath);
    if (journal.Inspect().Status != SessionJournalStatus.Missing) throw new InvalidOperationException("Existing journal blocks test.");
    var reader = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
    var all = reader.Capture(WindowsDisplayService.AllPaths).Targets
        .Where(t => t.Status == 0).DistinctBy(t => (t.AdapterLow, t.AdapterHigh, t.Id)).ToArray();
    var tv = all.Single(t => t.Id == 41015 && t.EdidManufacturer == 11596 && t.EdidProduct == 29031);
    var monitor = all.Single(t => t.Id == 24646 && t.EdidManufacturer == 58117 && t.EdidProduct == 9986);
    if (tv.DevicePath is null || monitor.DevicePath is null) throw new InvalidOperationException("Display paths missing.");
    var context = new RescueTransactionContext(Path.GetDirectoryName(journalPath)!);
    var display = new WindowsDisplayService(new WindowsDisplayOptions(tv.DevicePath, 11596, 29031, AllowWrites: true,
        InternalDisplay: new DisplaySelector(monitor.DevicePath, 58117, 9986)), context);
    var before = display.GetCurrent();
    if (before.Mode != DisplayMode.InternalOnly) throw new InvalidOperationException("Expected AOC-only baseline.");
    var baseline = WindowsAudioProbe.CaptureBaseline();
    var childEntry = new SessionJournalEntry(SessionJournalEntry.PendingState, DisplayMode.TvOnly, AudioMode.MoveToHdmi, DateTimeOffset.UtcNow)
    { RestoreDisplay = before, RestoreAudio = AudioOutput.Default, RestoreAudioBaseline = baseline };
    journal.Save(childEntry);
    var guard = new WindowsRescueGuard(rescuePath, journalPath, TimeSpan.FromMinutes(5), allowRecovery: true,
        transaction: context, allowExperimentalAudioRecovery: true);
    await using var lease = await guard.BeginAsync("audio hardware crash test");
    lease.ThrowIfUnavailable();
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(lease.FailureToken);
    await display.ApplyAsync(DisplayMode.TvOnly, linked.Token);
    lease.ThrowIfUnavailable();
    var audioTimer = Stopwatch.StartNew();
    while (true)
    {
        linked.Token.ThrowIfCancellationRequested();
        lease.ThrowIfUnavailable();
        var inventory = WindowsAudioProbe.Capture();
        var active = inventory.Endpoints.Where(e => e.State == 1 && e.FriendlyName == "SAMSUNG (3- HD Audio Driver for Display Audio)").ToArray();
        if (active.Length == 1 && inventory.Console == active[0].Id && inventory.Multimedia == active[0].Id && inventory.Communications == active[0].Id) break;
        if (audioTimer.Elapsed > TimeSpan.FromSeconds(15)) throw new InvalidOperationException("Samsung was not the default for all roles.");
        await Task.Delay(200, linked.Token);
    }
    Console.WriteLine("TV_APPLIED_WAITING_FOR_OPERATOR");
    await Console.Out.FlushAsync();
    await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token);
    return 0;
}
if (args[0] != "--tv-crash-confirmed") return 2;
if (journal.Inspect().Status != SessionJournalStatus.Missing) throw new InvalidOperationException("Existing journal blocks test.");
var decisionDir = Path.Combine(Path.GetDirectoryName(journalPath)!, "rescue-decisions");
var existing = Directory.Exists(decisionDir) ? Directory.GetFiles(decisionDir, "*.json").ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing test executable.");
var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
info.ArgumentList.Add("--child"); info.ArgumentList.Add(rescuePath);
using var parent = Process.Start(info) ?? throw new InvalidOperationException("Test parent did not start.");
try
{
using var readyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
var line = await parent.StandardOutput.ReadLineAsync(readyTimeout.Token);
if (line != "TV_APPLIED_WAITING_FOR_OPERATOR")
    throw new InvalidOperationException("TV apply was not confirmed; recovery may be pending. " + await parent.StandardError.ReadToEndAsync());
Console.WriteLine("TV_APPLIED_WAITING_FOR_OPERATOR: press Enter only after confirming TV image");
await Console.Out.FlushAsync();
if (await Console.In.ReadLineAsync() is null) throw new InvalidOperationException("Operator input lost; watchdog will recover on timeout.");
if (parent.HasExited) throw new InvalidOperationException("Test parent exited unexpectedly; check recovery.");
var entry = journal.Load() ?? throw new InvalidDataException("Pending journal missing.");
var files = Directory.GetFiles(decisionDir, "*.json").Where(f => !existing.Contains(f)).ToArray();
if (files.Length != 1 || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(files[0]), "N", out var id))
    throw new InvalidOperationException("Could not identify watchdog decision; no kill attempted.");
var hash = RescueDecisionStore.ComputeBaselineSha256(entry);
var decisions = new RescueDecisionStore(files[0]);
if ((await decisions.ReadAsync(id, hash))?.State != RescueDecisionState.Armed) throw new InvalidOperationException("Watchdog is not armed.");
parent.Kill(entireProcessTree: false);
await parent.WaitForExitAsync();
var timer = Stopwatch.StartNew();
RescueDecisionState? state = null;
while (timer.Elapsed < TimeSpan.FromSeconds(40))
{
    state = (await decisions.ReadAsync(id, hash))?.State;
    if (state == RescueDecisionState.Recovered) break;
    await Task.Delay(200);
}
if (state != RescueDecisionState.Recovered) throw new InvalidOperationException("Watchdog did not confirm recovery; journal retained.");
using var ownerAfter = DisplaySessionOwner.Acquire(journalPath);
var displayAfter = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
var expectedDisplay = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(entry.RestoreDisplay!.Topology!)!;
if (!DisplaySnapshotComparer.SemanticallyEqual(expectedDisplay, displayAfter.Capture(2))) throw new InvalidOperationException("Display restore mismatch; journal retained.");
var expectedAudio = entry.RestoreAudioBaseline!;
var actualAudio = WindowsAudioProbe.CaptureBaseline();
if (!string.Equals(expectedAudio.ConsoleEndpointId, actualAudio.ConsoleEndpointId, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(expectedAudio.MultimediaEndpointId, actualAudio.MultimediaEndpointId, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(expectedAudio.CommunicationsEndpointId, actualAudio.CommunicationsEndpointId, StringComparison.OrdinalIgnoreCase) ||
    expectedAudio.Endpoints.Any(e => !actualAudio.Endpoints.Any(a => string.Equals(e.EndpointId, a.EndpointId, StringComparison.OrdinalIgnoreCase) &&
        e.Muted == a.Muted && e.MasterVolume is float ev && a.MasterVolume is float av && Math.Abs(ev - av) <= 0.01f)))
    throw new InvalidOperationException("Audio restore mismatch; journal retained.");
journal.Clear();
Console.WriteLine("CRASH_RECOVERED: display, audio roles, volume and mute verified; journal cleared");
return 0;
}
finally
{
    // This process owns only the dedicated test child; its termination triggers the watchdog.
    if (!parent.HasExited) { parent.Kill(entireProcessTree: false); await parent.WaitForExitAsync(); }
}

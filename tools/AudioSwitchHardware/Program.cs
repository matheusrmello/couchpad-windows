using System.Text.Json;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows() || args.Length != 3 || args[0] is not ("--extend-switch-confirmed" or "--extend-inventory-confirmed")) return 2;
var journalPath = AppPaths.SessionJournalFile();
using var owner = DisplaySessionOwner.Acquire(journalPath);
var journal = new JsonSessionJournalStore(journalPath);
if (journal.Inspect().Status != SessionJournalStatus.Missing) throw new InvalidOperationException("Existing journal blocks test.");
var probe = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
var targets = probe.Capture(WindowsDisplayService.AllPaths).Targets.Where(t => t.Status == 0)
    .DistinctBy(t => (t.AdapterLow, t.AdapterHigh, t.Id)).ToArray();
var tv = targets.Single(t => t.Id == 41015 && t.EdidManufacturer == 11596 && t.EdidProduct == 29031);
var monitor = targets.Single(t => t.Id == 24646 && t.EdidManufacturer == 58117 && t.EdidProduct == 9986);
if (tv.DevicePath is null || monitor.DevicePath is null) throw new InvalidOperationException("Display paths missing.");
var stateDirectory = Path.GetDirectoryName(journalPath)!;
var transaction = new RescueTransactionContext(stateDirectory);
var display = new WindowsDisplayService(new WindowsDisplayOptions(tv.DevicePath, 11596, 29031, AllowWrites: true,
    InternalDisplay: new DisplaySelector(monitor.DevicePath, 58117, 9986)), transaction);
var audio = new WindowsAudioService(new WindowsAudioOptions(args[2], AllowWrites: true), transaction);
var before = display.GetCurrent();
if (before.Mode != DisplayMode.InternalOnly) throw new InvalidOperationException("Expected AOC-only baseline.");
var baseline = audio.CaptureBaseline();
if (baseline.ConsoleEndpointId is null || baseline.ConsoleEndpointId != baseline.MultimediaEndpointId || baseline.ConsoleEndpointId != baseline.CommunicationsEndpointId)
    throw new InvalidOperationException("Test requires one common baseline endpoint for all roles.");
var monitorAudio = new WindowsAudioService(new WindowsAudioOptions(baseline.ConsoleEndpointId, AllowWrites: true), transaction);
journal.Save(new SessionJournalEntry(SessionJournalEntry.PendingState, DisplayMode.Extend, AudioMode.MoveToHdmi, DateTimeOffset.UtcNow)
{ RestoreDisplay = before, RestoreAudio = AudioOutput.Default, RestoreAudioBaseline = baseline });
var guard = new WindowsAudioRescueGuard(Path.GetFullPath(args[1]), journalPath, TimeSpan.FromMinutes(3), transaction, allowRecovery: true);
await using var lease = await guard.BeginAsync("test audio switch");
using var linked = CancellationTokenSource.CreateLinkedTokenSource(lease.FailureToken);
try
{
    lease.ThrowIfUnavailable();
    await display.ApplyAsync(DisplayMode.Extend, linked.Token);
    lease.ThrowIfUnavailable();
    if (args[0] == "--extend-inventory-confirmed")
    {
        await Task.Delay(TimeSpan.FromSeconds(3), linked.Token);
        Console.WriteLine("EXTEND_AUDIO_INVENTORY=" + JsonSerializer.Serialize(WindowsAudioProbe.Capture()));
        await display.RestoreAsync(before, linked.Token);
        await audio.RestoreBaselineAsync(baseline, linked.Token);
        lease.ThrowIfUnavailable();
        var expected = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(before.Topology!)!;
        if (!DisplaySnapshotComparer.SemanticallyEqual(expected, display.Capture(2))) throw new InvalidOperationException("Display restore mismatch.");
        await lease.AbortAsync(linked.Token);
        journal.Clear();
        Console.WriteLine("INVENTORY_RESTORED: baseline roles, volume, mute and display verified");
        return 0;
    }
    await monitorAudio.ApplyAsync(AudioMode.MoveToHdmi, linked.Token);
    if (monitorAudio.GetCurrent() != AudioOutput.Hdmi) throw new InvalidOperationException("Monitor audio precondition failed.");
    await audio.ApplyAsync(AudioMode.MoveToHdmi, linked.Token);
    lease.ThrowIfUnavailable();
    if (audio.GetCurrent() != AudioOutput.Hdmi) throw new InvalidOperationException("TV audio did not become default.");
    Console.WriteLine("TV_AUDIO_ACTIVE: playing diagnostic tone; press Enter to restore");
    await Console.Out.FlushAsync();
    WindowsAudioTestTone.Play();
    if (await Console.In.ReadLineAsync(linked.Token) is null) throw new InvalidOperationException("Operator input lost.");
    await display.RestoreAsync(before, linked.Token);
    await audio.RestoreBaselineAsync(baseline, linked.Token);
    lease.ThrowIfUnavailable();
    var originalSnapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(before.Topology!)!;
    if (!DisplaySnapshotComparer.SemanticallyEqual(originalSnapshot, display.Capture(2))) throw new InvalidOperationException("Display restore mismatch.");
    await lease.AbortAsync(linked.Token);
    journal.Clear();
    Console.WriteLine("AUDIO_SWITCH_RESTORED: baseline roles, volume, mute and display verified");
    return 0;
}
catch
{
    // If anything is uncertain, the independent watchdog owns recovery.
    lease.Abandon();
    throw;
}

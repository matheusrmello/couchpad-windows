using System.Text.Json;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;
using CouchPad.Core.Launchers;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows() || args.Length != 3 || args[0] != "--missing-confirmed") return 2;
var journalPath = AppPaths.SessionJournalFile();
using var owner = DisplaySessionOwner.Acquire(journalPath);
var journal = new JsonSessionJournalStore(journalPath);
if (journal.Inspect().Status != SessionJournalStatus.Missing) throw new InvalidOperationException("Existing journal blocks the test.");
var targets = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0)).Capture(WindowsDisplayService.AllPaths).Targets
    .Where(t => t.Status == 0).DistinctBy(t => (t.AdapterLow, t.AdapterHigh, t.Id)).ToArray();
var tv = targets.Single(t => t.Id == 41015 && t.EdidManufacturer == 11596 && t.EdidProduct == 29031);
var monitor = targets.Single(t => t.Id == 24646 && t.EdidManufacturer == 58117 && t.EdidProduct == 9986);
if (tv.DevicePath is null || monitor.DevicePath is null) throw new InvalidOperationException("Display paths missing.");
var transaction = new RescueTransactionContext(Path.GetDirectoryName(journalPath)!);
var display = new WindowsDisplayService(new WindowsDisplayOptions(tv.DevicePath, 11596, 29031, AllowWrites: true,
    InternalDisplay: new DisplaySelector(monitor.DevicePath, 58117, 9986)), transaction);
var before = display.GetCurrent();
if (before.Mode != DisplayMode.InternalOnly) throw new InvalidOperationException("Expected monitor-only baseline.");
var audio = new WindowsAudioService(new WindowsAudioOptions(args[2], AllowWrites: true), transaction);
var baseline = audio.CaptureBaseline();
var guard = new WindowsAudioRescueGuard(Path.GetFullPath(args[1]), journalPath, TimeSpan.FromMinutes(3), transaction, allowRecovery: true);
var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal, rescueGuard: guard, launcher: new MissingLauncher());
await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, DisplayMode = DisplayMode.TvOnly, AudioMode = AudioMode.MoveToHdmi, LauncherEnabled = true });
try
{
    await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
    if (!coordinator.LauncherFailed || coordinator.State != AutomationState.Active || journal.Load()?.State != SessionJournalEntry.ActiveState)
        throw new InvalidOperationException("Launcher failure did not leave an active, recoverable session.");
    Console.WriteLine("MISSING_LAUNCHER_INJECTED_SESSION_ACTIVE: press Enter to restore");
    await Console.Out.FlushAsync();
    if (await Console.In.ReadLineAsync() is null) throw new InvalidOperationException("Operator input lost.");
}
finally { await coordinator.GamepadDisconnectedAsync(); }
var expected = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(before.Topology!)!;
var restored = WindowsAudioProbe.CaptureBaseline();
var audioMatches = string.Equals(baseline.ConsoleEndpointId, restored.ConsoleEndpointId, StringComparison.OrdinalIgnoreCase) &&
    string.Equals(baseline.MultimediaEndpointId, restored.MultimediaEndpointId, StringComparison.OrdinalIgnoreCase) &&
    string.Equals(baseline.CommunicationsEndpointId, restored.CommunicationsEndpointId, StringComparison.OrdinalIgnoreCase) &&
    baseline.Endpoints.All(e => restored.Endpoints.Any(a => string.Equals(e.EndpointId, a.EndpointId, StringComparison.OrdinalIgnoreCase) &&
        e.Muted == a.Muted && e.MasterVolume is float ev && a.MasterVolume is float av && Math.Abs(ev - av) <= .01f));
if (coordinator.IsRecoveryUncertain || journal.Inspect().Status != SessionJournalStatus.Missing || !audioMatches ||
    !DisplaySnapshotComparer.SemanticallyEqual(expected, display.Capture(2)))
    throw new InvalidOperationException("Recovery verification failed or is uncertain; stop testing.");
Console.WriteLine("MISSING_LAUNCHER_ROLLBACK_VERIFIED");
return 0;

sealed class MissingLauncher : ILauncherService
{
    public Task StartAsync(LauncherSelection selection, CancellationToken cancellationToken = default) =>
        Task.FromException(new FileNotFoundException("Injected missing launcher."));
    public Task CloseOwnedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

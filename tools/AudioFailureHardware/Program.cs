using System.Text.Json;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows() || args.Length != 3 || args[0] is not ("--failure-confirmed" or "--cancel-confirmed")) return 2;
var journalPath = AppPaths.SessionJournalFile();
using var owner = DisplaySessionOwner.Acquire(journalPath);
var journal = new JsonSessionJournalStore(journalPath);
if (journal.Inspect().Status != SessionJournalStatus.Missing) throw new InvalidOperationException("Existing journal blocks the test.");
var probe = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
var targets = probe.Capture(WindowsDisplayService.AllPaths).Targets.Where(t => t.Status == 0)
    .DistinctBy(t => (t.AdapterLow, t.AdapterHigh, t.Id)).ToArray();
var tv = targets.Single(t => t.Id == 41015 && t.EdidManufacturer == 11596 && t.EdidProduct == 29031);
var monitor = targets.Single(t => t.Id == 24646 && t.EdidManufacturer == 58117 && t.EdidProduct == 9986);
if (tv.DevicePath is null || monitor.DevicePath is null) throw new InvalidOperationException("Display paths missing.");
var transaction = new RescueTransactionContext(Path.GetDirectoryName(journalPath)!);
var display = new WindowsDisplayService(new WindowsDisplayOptions(tv.DevicePath, 11596, 29031, AllowWrites: true,
    InternalDisplay: new DisplaySelector(monitor.DevicePath, 58117, 9986)), transaction);
var before = display.GetCurrent();
if (before.Mode != DisplayMode.InternalOnly) throw new InvalidOperationException("Expected AOC-only baseline.");
var actualAudio = new WindowsAudioService(new WindowsAudioOptions(args[2], AllowWrites: true), transaction);
var baseline = actualAudio.CaptureBaseline();
var audio = new PausedAudio(actualAudio, args[0] == "--cancel-confirmed");
var guard = new WindowsAudioRescueGuard(Path.GetFullPath(args[1]), journalPath, TimeSpan.FromMinutes(3), transaction, allowRecovery: true);
var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal, rescueGuard: guard);
await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, DisplayMode = DisplayMode.TvOnly, AudioMode = AudioMode.MoveToHdmi });
var activation = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
await audio.Ready.Task.WaitAsync(TimeSpan.FromSeconds(45));
Console.WriteLine("AUDIO_APPLIED_WAITING_FOR_OPERATOR: press Enter to trigger test");
await Console.Out.FlushAsync();
if (await Console.In.ReadLineAsync() is null) throw new InvalidOperationException("Operator input lost; watchdog recovery required.");
if (args[0] == "--cancel-confirmed") await coordinator.GamepadDisconnectedAsync();
else audio.Release.TrySetResult();
await activation;
var expectedState = args[0] == "--cancel-confirmed" ? AutomationState.WaitingForGamepad : AutomationState.Error;
var expected = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(before.Topology!)!;
var restored = WindowsAudioProbe.CaptureBaseline();
var audioMatches = string.Equals(baseline.ConsoleEndpointId, restored.ConsoleEndpointId, StringComparison.OrdinalIgnoreCase) &&
    string.Equals(baseline.MultimediaEndpointId, restored.MultimediaEndpointId, StringComparison.OrdinalIgnoreCase) &&
    string.Equals(baseline.CommunicationsEndpointId, restored.CommunicationsEndpointId, StringComparison.OrdinalIgnoreCase) &&
    baseline.Endpoints.All(e => restored.Endpoints.Any(a => string.Equals(e.EndpointId, a.EndpointId, StringComparison.OrdinalIgnoreCase) &&
        e.Muted == a.Muted && e.MasterVolume is float ev && a.MasterVolume is float av && Math.Abs(ev - av) <= .01f));
if (coordinator.State != expectedState || coordinator.IsRecoveryUncertain || journal.Inspect().Status != SessionJournalStatus.Missing ||
    !audioMatches || !DisplaySnapshotComparer.SemanticallyEqual(expected, display.Capture(2)))
    throw new InvalidOperationException("Recovery verification failed or is uncertain; stop testing.");
Console.WriteLine("AUDIO_ROLLBACK_VERIFIED: state, display, three roles, volume, mute and journal");
return 0;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
sealed class PausedAudio(WindowsAudioService inner, bool cancel) : IAudioService, IAudioBaselineService
{
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public AudioOutput GetCurrent() => inner.GetCurrent();
    public AudioBaseline CaptureBaseline() => inner.CaptureBaseline();
    public Task RestoreBaselineAsync(AudioBaseline baseline, CancellationToken cancellationToken = default) => inner.RestoreBaselineAsync(baseline, cancellationToken);
    public Task RestoreAsync(AudioOutput state, CancellationToken cancellationToken = default) => inner.RestoreAsync(state, cancellationToken);
    public async Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken cancellationToken = default)
    {
        await inner.ApplyAsync(mode, cancellationToken);
        Ready.TrySetResult();
        if (cancel) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        else await Release.Task.WaitAsync(cancellationToken);
        throw new InvalidOperationException("Injected audio failure after apply");
    }
}

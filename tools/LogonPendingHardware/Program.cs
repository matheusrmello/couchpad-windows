using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;
using CouchPad.Core.Settings;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows() || args is not ["--prepare", var evidencePath]) return 2;
var journalPath = AppPaths.SessionJournalFile();
evidencePath = Path.GetFullPath(evidencePath);
if (Path.GetDirectoryName(evidencePath)!.Equals(AppPaths.StateDirectory(), StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Keep test evidence outside the production state directory.");

using var owner = DisplaySessionOwner.Acquire(journalPath);
var journal = new JsonSessionJournalStore(journalPath);
if (journal.Inspect().Status != SessionJournalStatus.Missing || File.Exists(evidencePath))
    throw new InvalidOperationException("Existing journal or evidence blocks preparation.");

var settings = new JsonAppSettingsStore().Load();
var display = new WindowsDisplayService(new WindowsDisplayOptions(
    settings.TvDevicePath ?? throw new InvalidOperationException("TV selector missing."),
    settings.TvEdidManufacturer!.Value, settings.TvEdidProduct!.Value,
    InternalDisplay: new DisplaySelector(
        settings.InternalDevicePath ?? throw new InvalidOperationException("Monitor selector missing."),
        settings.InternalEdidManufacturer!.Value, settings.InternalEdidProduct!.Value)));
if (display.GetCurrent().Mode != DisplayMode.InternalOnly)
    throw new InvalidOperationException("Expected monitor-only baseline.");

var transaction = new RescueTransactionContext(AppPaths.StateDirectory());
var audio = new WindowsAudioService(new WindowsAudioOptions(
    settings.TvAudioEndpointId ?? throw new InvalidOperationException("TV audio selector missing.")), transaction);
var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal,
    rescueGuard: new StopBeforeApply(journal, journalPath, evidencePath));
await coordinator.UpdateSettingsAsync(new AutomationSettings
{
    Enabled = true,
    DisplayMode = DisplayMode.TvOnly,
    AudioMode = AudioMode.MoveToHdmi,
});
await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
return 1;

sealed class StopBeforeApply(JsonSessionJournalStore journal, string journalPath, string evidencePath) : IAudioRescueGuard
{
    public Task<IRescueLease> BeginAsync(string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = journal.Load();
        if (entry is not { State: SessionJournalEntry.PendingState, RestoreDisplay.Topology: not null, RestoreAudioBaseline: not null })
            throw new InvalidDataException("Expected pending journal with real display and audio baselines.");
        File.Copy(journalPath, evidencePath, overwrite: false);
        Console.WriteLine("PENDING_BEFORE_APPLY");
        Console.Out.Flush();
        // Simulate process death at the boundary before watchdog startup and before any hardware write.
        Environment.Exit(0);
        throw new InvalidOperationException("Process exit returned unexpectedly.");
    }
}

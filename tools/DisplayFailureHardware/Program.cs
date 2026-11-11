using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (args.Length != 8 || args[0] != "--hardware-confirmed")
{
    Console.Error.WriteLine("Usage: --hardware-confirmed <rescue.exe> <tv-path> <tv-manufacturer> <tv-product> <monitor-path> <monitor-manufacturer> <monitor-product>");
    return 2;
}
var journalPath = AppPaths.SessionJournalFile();
using var owner = DisplaySessionOwner.Acquire(journalPath);
var journal = new JsonSessionJournalStore(journalPath);
if (journal.Inspect().Status != SessionJournalStatus.Missing) throw new InvalidOperationException("Existing journal blocks the test.");
var options = new WindowsDisplayOptions(args[2], ushort.Parse(args[3]), ushort.Parse(args[4]), AllowWrites: true,
    InternalDisplay: new DisplaySelector(args[5], ushort.Parse(args[6]), ushort.Parse(args[7])));
var context = new RescueTransactionContext(Path.GetDirectoryName(journalPath)!);
var display = new WindowsDisplayService(options, context);
var before = display.GetCurrent();
if (before.Mode != DisplayMode.InternalOnly) throw new InvalidOperationException("Expected internal-only baseline.");
var coordinator = new AutomationCoordinator(display, new FailingAudio(), new SystemDelayScheduler(), journal,
    rescueGuard: new WindowsRescueGuard(args[1], journalPath, TimeSpan.FromSeconds(60), true, context));
await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, DisplayMode = DisplayMode.TvOnly });
await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
var after = display.GetCurrent();
var snapshotBefore = System.Text.Json.JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(before.Topology!)!;
var snapshotAfter = System.Text.Json.JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(after.Topology!)!;
var semanticMatch = DisplaySnapshotComparer.SemanticallyEqual(snapshotBefore, snapshotAfter);
var journalStatus = journal.Inspect().Status;
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { coordinator.State, coordinator.LastError, Before = before.Mode, After = after.Mode, SemanticMatch = semanticMatch, Journal = journalStatus }));
return coordinator.State == AutomationState.Error && coordinator.LastError == FailingAudio.Expected && semanticMatch && journalStatus == SessionJournalStatus.Missing ? 0 : 1;

sealed class FailingAudio : IAudioService
{
    public const string Expected = "Injected test failure after display apply";
    public AudioOutput GetCurrent() => AudioOutput.Default;
    public async Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("DISPLAY_APPLIED_WAITING_TO_INJECT_FAILURE");
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        throw new InvalidOperationException(Expected);
    }
    public Task RestoreAsync(AudioOutput state, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

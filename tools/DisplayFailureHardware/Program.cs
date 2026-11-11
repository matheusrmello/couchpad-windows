using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (args.Length != 8 || args[0] is not ("--hardware-confirmed" or "--hardware-cancel" or "--hardware-disconnect"))
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
var cancelScenario = args[0] == "--hardware-cancel";
var disconnectScenario = args[0] == "--hardware-disconnect";
var audio = new FailingAudio(cancelScenario, disconnectScenario ? display : null, options);
var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal,
    rescueGuard: new WindowsRescueGuard(args[1], journalPath, TimeSpan.FromSeconds(60), true, context));
await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, DisplayMode = disconnectScenario ? DisplayMode.Extend : DisplayMode.TvOnly });
var activation = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
if (cancelScenario)
{
    await audio.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
    await Task.Delay(TimeSpan.FromSeconds(3));
    await coordinator.GamepadDisconnectedAsync();
}
await activation;
var afterSnapshot = display.Capture(2);
var snapshotBefore = System.Text.Json.JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(before.Topology!)!;
var semanticMatch = DisplaySnapshotComparer.SemanticallyEqual(snapshotBefore, afterSnapshot);
var journalStatus = journal.Inspect().Status;
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { coordinator.State, coordinator.LastError, Before = before.Mode, SemanticMatch = semanticMatch, Journal = journalStatus }));
var expectedState = cancelScenario ? coordinator.State == AutomationState.WaitingForGamepad : coordinator.State == AutomationState.Error && coordinator.LastError == FailingAudio.Expected;
return expectedState && semanticMatch && journalStatus == SessionJournalStatus.Missing ? 0 : 1;

sealed class FailingAudio(bool cancelScenario, WindowsDisplayService? disconnectDisplay, WindowsDisplayOptions options) : IAudioService
{
    public const string Expected = "Injected test failure after display apply";
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public AudioOutput GetCurrent() => AudioOutput.Default;
    public async Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("DISPLAY_APPLIED_WAITING_TO_INJECT_FAILURE");
        Entered.TrySetResult();
        if (cancelScenario) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        if (disconnectDisplay is not null)
        {
            Console.WriteLine("WAITING_FOR_PHYSICAL_TV_DISCONNECT");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var targets = disconnectDisplay.Capture(WindowsDisplayService.AllPaths).Targets;
                try { WindowsDisplayService.SelectTarget(targets, options.DevicePath, options.EdidManufacturer, options.EdidProduct, options.ExactName); }
                catch (InvalidOperationException) { break; }
                await Task.Delay(200, deadline.Token);
            }
        }
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        throw new InvalidOperationException(Expected);
    }
    public Task RestoreAsync(AudioOutput state, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

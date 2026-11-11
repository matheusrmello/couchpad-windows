using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

// Explicit, operator-present hardware experiment: restore the current baseline,
// without first changing the display or audio selection. Keep journal on failure.
if (!OperatingSystem.IsWindows() || args.Length != 2 || args[0] != "--current-baseline-confirmed") return 2;
var rescuePath = Path.GetFullPath(args[1]);
var journalPath = AppPaths.SessionJournalFile();
using var owner = DisplaySessionOwner.Acquire(journalPath);
var journal = new JsonSessionJournalStore(journalPath);
if (journal.Inspect().Status != SessionJournalStatus.Missing) throw new InvalidOperationException("Existing journal blocks this test.");
var display = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
var snapshot = display.Capture(2);
if (snapshot.Targets.Count != 1 || snapshot.Targets[0].FriendlyName != "27G2G4")
    throw new InvalidOperationException("Expected the AOC as the sole active display.");
var audio = WindowsAudioProbe.CaptureBaseline();
AudioRecoveryTransaction.ValidateBaseline(audio);
var entry = new SessionJournalEntry(SessionJournalEntry.PendingState, DisplayMode.InternalOnly, AudioMode.KeepCurrent, DateTimeOffset.UtcNow)
{
    RestoreDisplay = new DisplayState(DisplayMode.InternalOnly, true, false) { Topology = JsonSerializer.Serialize(snapshot) },
    RestoreAudio = AudioOutput.Default,
    RestoreAudioBaseline = audio
};
journal.Save(entry);
var transactionId = Guid.NewGuid();
var hash = RescueDecisionStore.ComputeBaselineSha256(entry);
var decisionPath = Path.Combine(Path.GetDirectoryName(journalPath)!, "rescue-decisions", transactionId.ToString("N") + ".json");
var decisions = new RescueDecisionStore(decisionPath);
await decisions.InitializeAsync(transactionId, hash);
var pipeName = "CouchPad-rescue-" + Guid.NewGuid().ToString("N");
using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
using var self = Process.GetCurrentProcess();
var start = new ProcessStartInfo(rescuePath) { UseShellExecute = false, CreateNoWindow = true };
foreach (var argument in new[] { pipeName, journalPath, self.Id.ToString(), self.StartTime.ToUniversalTime().Ticks.ToString(), "30000", transactionId.ToString("D"), hash, decisionPath })
    start.ArgumentList.Add(argument);
using var child = Process.Start(start) ?? throw new InvalidOperationException("Rescue did not start; journal retained.");
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
await pipe.WaitForConnectionAsync(deadline.Token);
using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, true);
if (await reader.ReadLineAsync(deadline.Token) != "READY") throw new InvalidOperationException("Rescue not ready; journal retained.");
Console.WriteLine("READY: requesting baseline recovery by disconnecting test pipe");
pipe.Disconnect();
await child.WaitForExitAsync(deadline.Token);
var decision = await decisions.ReadAsync(transactionId, hash, deadline.Token);
if (child.ExitCode != 0 || decision?.State != RescueDecisionState.Recovered)
    throw new InvalidOperationException("Recovery failed or is uncertain; journal retained.");
if (!DisplaySnapshotComparer.SemanticallyEqual(snapshot, display.Capture(2)))
    throw new InvalidOperationException("Display differs after recovery; journal retained.");
var after = WindowsAudioProbe.CaptureBaseline();
if (!string.Equals(audio.ConsoleEndpointId, after.ConsoleEndpointId, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(audio.MultimediaEndpointId, after.MultimediaEndpointId, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(audio.CommunicationsEndpointId, after.CommunicationsEndpointId, StringComparison.OrdinalIgnoreCase) ||
    audio.Endpoints.Any(expected => !after.Endpoints.Any(actual =>
        string.Equals(expected.EndpointId, actual.EndpointId, StringComparison.OrdinalIgnoreCase) &&
        expected.Muted == actual.Muted && expected.MasterVolume is float e && actual.MasterVolume is float a && Math.Abs(e - a) <= 0.01f)))
    throw new InvalidOperationException("Audio differs after recovery; journal retained.");
journal.Clear();
Console.WriteLine("CURRENT_BASELINE_RECOVERED: display, roles, volume and mute verified; journal cleared");
return 0;

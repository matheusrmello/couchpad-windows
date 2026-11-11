using System.Text.Json;
using CouchPad.Core.Automation;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows() || args.Length != 2 || args[0] != "--reconcile-recovered" || !Guid.TryParse(args[1], out var id)) return 2;
var journalPath = AppPaths.SessionJournalFile();
using var owner = DisplaySessionOwner.Acquire(journalPath);
var journal = new JsonSessionJournalStore(journalPath);
var entry = journal.Load() ?? throw new InvalidDataException("Journal missing.");
if (entry.State != SessionJournalEntry.PendingState || entry.RestoreAudioBaseline is null || entry.RestoreDisplay?.Topology is null)
    throw new InvalidDataException("Expected a pending journal with complete baseline.");
var decisionPath = Path.Combine(Path.GetDirectoryName(journalPath)!, "rescue-decisions", id.ToString("N") + ".json");
var decision = await new RescueDecisionStore(decisionPath).ReadAsync(id, RescueDecisionStore.ComputeBaselineSha256(entry));
if (decision?.State != RescueDecisionState.Recovered) throw new InvalidDataException("Recovery is not confirmed.");
var expected = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(entry.RestoreDisplay.Topology)!;
var display = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
if (!DisplaySnapshotComparer.SemanticallyEqual(expected, display.Capture(2))) throw new InvalidDataException("Display baseline mismatch.");
var audio = WindowsAudioProbe.CaptureBaseline();
var baseline = entry.RestoreAudioBaseline;
if (!string.Equals(baseline.ConsoleEndpointId, audio.ConsoleEndpointId, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(baseline.MultimediaEndpointId, audio.MultimediaEndpointId, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(baseline.CommunicationsEndpointId, audio.CommunicationsEndpointId, StringComparison.OrdinalIgnoreCase) ||
    baseline.Endpoints.Any(e => !audio.Endpoints.Any(a => string.Equals(e.EndpointId, a.EndpointId, StringComparison.OrdinalIgnoreCase) &&
        e.Muted == a.Muted && e.MasterVolume is float ev && a.MasterVolume is float av && Math.Abs(ev - av) <= 0.01f)))
    throw new InvalidDataException("Audio baseline mismatch.");
journal.Clear();
Console.WriteLine("RECOVERED_JOURNAL_RECONCILED: display, roles, volume and mute verified");
return 0;

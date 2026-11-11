using System.Text.Json;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;
using CouchPad.Core.Automation;
if (args.Length < 3) throw new ArgumentException("Usage: DisplayProbe <tv-path> <manufacturer> <product> --internal <monitor-path> <manufacturer> <product> [--validate] [--compare-current]");
var internalIndex = Array.IndexOf(args, "--internal");
if (internalIndex < 3 || internalIndex + 3 >= args.Length) throw new ArgumentException("An explicit internal display selector is required.");
var internalSelector = new DisplaySelector(args[internalIndex + 1], ushort.Parse(args[internalIndex + 2]), ushort.Parse(args[internalIndex + 3]));
var options = new WindowsDisplayOptions(args[0], ushort.Parse(args[1]), ushort.Parse(args[2]), InternalDisplay: internalSelector);
var service = new WindowsDisplayService(options);
var baseline = service.GetCurrent();
var all = service.Capture(WindowsDisplayService.AllPaths);
var selected = WindowsDisplayService.SelectTarget(all.Targets, options.DevicePath, options.EdidManufacturer, options.EdidProduct, options.ExactName);
var results = new Dictionary<string, int>();
if (args.Contains("--validate"))
{
    if (baseline.Mode != DisplayMode.InternalOnly) throw new InvalidOperationException("Validation requires the expected internal-only baseline.");
    var internalDisplay = WindowsDisplayService.SelectTarget(all.Targets, internalSelector.DevicePath, internalSelector.EdidManufacturer, internalSelector.EdidProduct);
    var snapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(baseline.Topology!)!;
    foreach (var mode in new[] { DisplayMode.TvOnly, DisplayMode.Extend })
        results.Add(mode.ToString(), DisplayTopologyValidator.ValidateProposal(DisplayTopologyPlanner.Create(snapshot, all, selected, internalDisplay, mode)));
}
if (args.Contains("--validate-invalid-target"))
{
    var internalDisplay = WindowsDisplayService.SelectTarget(all.Targets, internalSelector.DevicePath, internalSelector.EdidManufacturer, internalSelector.EdidProduct);
    var snapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(baseline.Topology!)!;
    var proposal = DisplayTopologyPlanner.Create(snapshot, all, selected, internalDisplay, DisplayMode.TvOnly);
    // Corrupt only the proposed target identifier; SDC_VALIDATE does not apply it.
    BitConverter.GetBytes(uint.MaxValue).CopyTo(proposal.Paths, 28);
    results.Add("InvalidTarget", DisplayTopologyValidator.ValidateProposal(proposal));
}
if (args.Contains("--validate-impossible-mode"))
{
    var snapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(baseline.Topology!)!;
    var modes = Convert.FromBase64String(snapshot.ModesBase64);
    var sourceOffset = Enumerable.Range(0, modes.Length / 64).Select(i => i * 64)
        .First(i => BitConverter.ToUInt32(modes, i) == 1);
    BitConverter.GetBytes(uint.MaxValue).CopyTo(modes, sourceOffset + 16);
    BitConverter.GetBytes(uint.MaxValue).CopyTo(modes, sourceOffset + 20);
    results.Add("ImpossibleMode", DisplayTopologyValidator.ValidateSnapshot(snapshot with { ModesBase64 = Convert.ToBase64String(modes) }));
}
bool? comparison = null;
bool? journalComparison = null;
string? journalBaselineHash = null;
var journalIndex = Array.IndexOf(args, "--compare-journal");
if (journalIndex >= 0)
{
    if (journalIndex + 1 >= args.Length) throw new ArgumentException("Missing journal path.");
    var entry = new JsonSessionJournalStore(args[journalIndex + 1]).Load() ?? throw new InvalidOperationException("Journal unavailable.");
    var expected = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(entry.RestoreDisplay?.Topology ?? throw new InvalidOperationException("Missing journal baseline."))!;
    var current = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(baseline.Topology!)!;
    journalComparison = DisplaySnapshotComparer.SemanticallyEqual(expected, current);
    journalBaselineHash = RescueDecisionStore.ComputeBaselineSha256(entry);
}
if (args.Contains("--compare-current"))
{
    var first = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(baseline.Topology!)!;
    var second = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(service.GetCurrent().Topology!)!;
    comparison = DisplaySnapshotComparer.SemanticallyEqual(first, second);
}
Console.WriteLine(JsonSerializer.Serialize(new { State = new { baseline.Mode, baseline.PrimaryActive, baseline.TvActive }, Baseline = JsonSerializer.Deserialize<JsonElement>(baseline.Topology!), AllPaths = all, Selected = selected, Validation = results, Comparison = comparison, JournalComparison = journalComparison, JournalBaselineHash = journalBaselineHash }));

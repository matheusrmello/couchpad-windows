using System.Text.Json;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;
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
    foreach (var mode in new[] { DisplayMode.TvOnly, DisplayMode.Extend, DisplayMode.Mirror })
        results.Add(mode.ToString(), DisplayTopologyValidator.ValidateProposal(DisplayTopologyPlanner.Create(snapshot, all, selected, internalDisplay, mode)));
}
bool? comparison = null;
if (args.Contains("--compare-current"))
{
    var first = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(baseline.Topology!)!;
    var second = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(service.GetCurrent().Topology!)!;
    comparison = DisplaySnapshotComparer.SemanticallyEqual(first, second);
}
Console.WriteLine(JsonSerializer.Serialize(new { State = new { baseline.Mode, baseline.PrimaryActive, baseline.TvActive }, Baseline = JsonSerializer.Deserialize<JsonElement>(baseline.Topology!), AllPaths = all, Selected = selected, Validation = results, Comparison = comparison }));

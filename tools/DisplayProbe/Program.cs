using System.Text.Json;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;
if (args.Length < 3) throw new ArgumentException("Usage: DisplayProbe <device-path> <edid-manufacturer> <edid-product> [exact-name] [--validate]");
var options = new WindowsDisplayOptions(args[0], ushort.Parse(args[1]), ushort.Parse(args[2]), args.Length >= 4 && args[3] != "--validate" ? args[3] : null);
var service = new WindowsDisplayService(options);
var baseline = service.GetCurrent();
var all = service.Capture(WindowsDisplayService.AllPaths);
var selected = WindowsDisplayService.SelectTarget(all.Targets, options.DevicePath, options.EdidManufacturer, options.EdidProduct, options.ExactName);
var results = new Dictionary<string, int>();
if (args.Contains("--validate"))
{
    if (baseline.Mode != DisplayMode.InternalOnly) throw new InvalidOperationException("Validation requires the expected internal-only baseline.");
    var snapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(baseline.Topology!)!;
    foreach (var mode in new[] { DisplayMode.TvOnly, DisplayMode.Extend, DisplayMode.Mirror })
        results.Add(mode.ToString(), DisplayTopologyValidator.ValidateProposal(DisplayTopologyPlanner.Create(snapshot, all, selected, mode)));
}
Console.WriteLine(JsonSerializer.Serialize(new { State = new { baseline.Mode, baseline.PrimaryActive, baseline.TvActive }, Baseline = JsonSerializer.Deserialize<JsonElement>(baseline.Topology!), AllPaths = all, Selected = selected, Validation = results }));

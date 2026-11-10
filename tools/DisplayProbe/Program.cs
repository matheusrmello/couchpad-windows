using System.Text.Json;
using CouchPad.Platform.Windows;
if (args.Length < 3) throw new ArgumentException("Usage: DisplayProbe <device-path> <edid-manufacturer> <edid-product> [exact-name]");
var options = new WindowsDisplayOptions(args[0], ushort.Parse(args[1]), ushort.Parse(args[2]), args.Length >= 4 ? args[3] : null);
var service = new WindowsDisplayService(options);
var baseline = service.GetCurrent();
var all = service.Capture(WindowsDisplayService.AllPaths);
var selected = args.Length >= 3 ? WindowsDisplayService.SelectTarget(all.Targets, args[0], ushort.Parse(args[1]), ushort.Parse(args[2]), args.Length >= 4 ? args[3] : null) : null;
Console.WriteLine(JsonSerializer.Serialize(new { State = new { baseline.Mode, baseline.PrimaryActive, baseline.TvActive }, Baseline = JsonSerializer.Deserialize<JsonElement>(baseline.Topology!), AllPaths = all, Selected = selected }));


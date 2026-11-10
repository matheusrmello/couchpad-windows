using System.Text.Json;
using CouchPad.Platform.Windows;
var service = new WindowsDisplayService();
var baseline = service.GetCurrent();
var all = service.Capture(WindowsDisplayService.AllPaths);
var selected = args.Length >= 3 ? WindowsDisplayService.SelectTarget(all.Targets, args[0], ushort.Parse(args[1]), ushort.Parse(args[2]), args.Length >= 4 ? args[3] : null) : null;
Console.WriteLine(JsonSerializer.Serialize(new { Baseline = JsonSerializer.Deserialize<JsonElement>(baseline.Topology!), AllPaths = all, Selected = selected }));

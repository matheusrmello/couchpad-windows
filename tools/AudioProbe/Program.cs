using System.Text.Json;
using CouchPad.Platform.Windows;

Console.WriteLine(JsonSerializer.Serialize(args.Contains("--baseline") ? (object)WindowsAudioProbe.CaptureBaseline() : WindowsAudioProbe.Capture()));

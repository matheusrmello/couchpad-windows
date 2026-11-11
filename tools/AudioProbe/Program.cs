using System.Text.Json;
using CouchPad.Platform.Windows;

Console.WriteLine(JsonSerializer.Serialize(WindowsAudioProbe.Capture()));

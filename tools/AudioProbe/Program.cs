using System.Text.Json;
using CouchPad.Platform.Windows;

if (args.Contains("--policy-probe"))
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
    WindowsAudioRecoveryBackend.CheckPolicyConfigAvailable();
    Console.WriteLine("POLICY_ACTIVATION_OK (no writes)");
}
else if (args.Contains("--backend-read"))
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
    var baseline = WindowsAudioProbe.CaptureBaseline();
    var backend = new WindowsAudioRecoveryBackend();
    foreach (var role in Enum.GetValues<AudioRole>())
    {
        var expected = role switch { AudioRole.Console => baseline.ConsoleEndpointId, AudioRole.Multimedia => baseline.MultimediaEndpointId, _ => baseline.CommunicationsEndpointId };
        if (!string.Equals(expected, backend.GetDefault(role), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Default changed during probe.");
    }
    foreach (var endpoint in baseline.Endpoints)
    {
        if (!backend.IsActive(endpoint.EndpointId)) throw new InvalidOperationException("Endpoint is unavailable.");
        var actual = backend.GetVolumeAndMute(endpoint.EndpointId);
        if (actual.Volume != endpoint.MasterVolume || actual.Muted != endpoint.Muted) throw new InvalidOperationException("Endpoint state changed during probe.");
    }
    Console.WriteLine("BACKEND_READ_OK (no writes)");
}
else Console.WriteLine(JsonSerializer.Serialize(args.Contains("--baseline") ? (object)WindowsAudioProbe.CaptureBaseline() : WindowsAudioProbe.Capture()));

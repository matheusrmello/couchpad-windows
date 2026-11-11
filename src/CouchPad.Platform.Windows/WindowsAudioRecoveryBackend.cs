using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CouchPad.Platform.Windows;

/// <summary>Audio recovery primitives. Mutations require an explicit gate and are not yet composed into production.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAudioRecoveryBackend(bool allowWrites = false) : IAudioRecoveryBackend
{
    /// <summary>Checks COM activation only; does not invoke the undocumented mutation method.</summary>
    public static void CheckPolicyConfigAvailable()
    {
        var policy = (IPolicyConfig)new PolicyConfigClient();
        Marshal.ReleaseComObject(policy);
    }

    public bool IsActive(string endpointId) => WindowsAudioProbe.Capture().Endpoints.Any(e =>
        e.State == 1 && string.Equals(e.Id, endpointId, StringComparison.OrdinalIgnoreCase));

    public string? GetDefault(AudioRole role)
    {
        ValidateRole(role);
        var enumerator = (WindowsAudioProbe.IMMDeviceEnumerator)new WindowsAudioProbe.MMDeviceEnumerator();
        try { return WindowsAudioProbe.GetDefault(enumerator, (int)role); }
        finally { Marshal.ReleaseComObject(enumerator); }
    }

    public void SetDefault(string endpointId, AudioRole role)
    {
        RequireWrites();
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        ValidateRole(role);
        if (!IsActive(endpointId)) throw new InvalidOperationException("Audio endpoint is unavailable.");
        var policy = (IPolicyConfig)new PolicyConfigClient();
        try { WindowsAudioProbe.Check(policy.SetDefaultEndpoint(endpointId, (int)role)); }
        finally { Marshal.ReleaseComObject(policy); }
    }

    public void SetVolumeAndMute(string endpointId, float volume, bool muted)
    {
        RequireWrites();
        if (!float.IsFinite(volume) || volume is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(volume));
        WithVolume(endpointId, endpoint =>
        {
            WindowsAudioProbe.Check(endpoint.SetMasterVolumeLevelScalar(volume, IntPtr.Zero));
            WindowsAudioProbe.Check(endpoint.SetMute(muted, IntPtr.Zero));
            return 0;
        });
    }

    public (float Volume, bool Muted) GetVolumeAndMute(string endpointId) => WithVolume(endpointId, endpoint =>
    {
        WindowsAudioProbe.Check(endpoint.GetMasterVolumeLevelScalar(out var volume));
        WindowsAudioProbe.Check(endpoint.GetMute(out var muted));
        return (volume, muted);
    });

    private static T WithVolume<T>(string endpointId, Func<WindowsAudioProbe.IAudioEndpointVolume, T> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        var enumerator = (WindowsAudioProbe.IMMDeviceEnumerator)new WindowsAudioProbe.MMDeviceEnumerator();
        try
        {
            WindowsAudioProbe.Check(enumerator.GetDevice(endpointId, out var device));
            try
            {
                WindowsAudioProbe.Check(device.GetState(out var state));
                if (state != 1) throw new InvalidOperationException("Audio endpoint is unavailable.");
                var iid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                WindowsAudioProbe.Check(device.Activate(ref iid, 23, IntPtr.Zero, out var instance));
                try { return action((WindowsAudioProbe.IAudioEndpointVolume)instance); }
                finally { Marshal.ReleaseComObject(instance); }
            }
            finally { Marshal.ReleaseComObject(device); }
        }
        finally { Marshal.ReleaseComObject(enumerator); }
    }

    private void RequireWrites() { if (!allowWrites) throw new NotSupportedException("Audio writes are disabled."); }
    private static void ValidateRole(AudioRole role) { if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role)); }

    // Undocumented Windows interface, explicitly authorized as an isolated exception (D047).
    // Unused slots are placeholders only; they must never be called.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient { }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        void Slot0(); void Slot1(); void Slot2(); void Slot3(); void Slot4();
        void Slot5(); void Slot6(); void Slot7(); void Slot8(); void Slot9();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string endpointId, int role);
    }
}

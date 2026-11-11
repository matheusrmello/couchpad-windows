using System.Runtime.InteropServices;

namespace CouchPad.Platform.Windows;

/// <summary>Read-only Core Audio endpoint inventory; does not change defaults.</summary>
public static class WindowsAudioProbe
{
    public sealed record Endpoint(string Id, uint State);
    public sealed record Inventory(IReadOnlyList<Endpoint> Endpoints, string? Console, string? Multimedia, string? Communications);

    public static Inventory Capture()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            Check(enumerator.EnumAudioEndpoints(0, 0xF, out var collection));
            try
            {
                Check(collection.GetCount(out var count));
                var endpoints = new List<Endpoint>();
                for (uint i = 0; i < count; i++)
                {
                    Check(collection.Item(i, out var device));
                    try { Check(device.GetId(out var id)); Check(device.GetState(out var state)); endpoints.Add(new(id, state)); }
                    finally { Marshal.ReleaseComObject(device); }
                }
                return new(endpoints, GetDefault(enumerator, 0), GetDefault(enumerator, 1), GetDefault(enumerator, 2));
            }
            finally { Marshal.ReleaseComObject(collection); }
        }
        finally { Marshal.ReleaseComObject(enumerator); }
    }

    private static string? GetDefault(IMMDeviceEnumerator enumerator, int role)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var hr = enumerator.GetDefaultAudioEndpoint(0, role, out var device);
        if (hr == unchecked((int)0x80070490)) return null;
        Check(hr);
        try { Check(device.GetId(out var id)); return id; }
        finally { Marshal.ReleaseComObject(device); }
    }

    private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint states, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate();
        [PreserveSig] int OpenPropertyStore();
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
}

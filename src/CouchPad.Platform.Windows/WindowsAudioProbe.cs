using System.Runtime.InteropServices;

namespace CouchPad.Platform.Windows;

/// <summary>Read-only Core Audio endpoint inventory; does not change defaults.</summary>
public static class WindowsAudioProbe
{
    public sealed record Endpoint(string Id, uint State, string? FriendlyName);
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
                    try { Check(device.GetId(out var id)); Check(device.GetState(out var state)); endpoints.Add(new(id, state, GetFriendlyName(device))); }
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

    private static string? GetFriendlyName(IMMDevice device)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        Check(device.OpenPropertyStore(0, out var store));
        try
        {
            var key = new PropertyKey { FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), PropertyId = 14 };
            Check(store.GetValue(ref key, out var value));
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : null; }
            finally { Check(PropVariantClear(ref value)); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid FormatId; public uint PropertyId; }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);

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
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }
}

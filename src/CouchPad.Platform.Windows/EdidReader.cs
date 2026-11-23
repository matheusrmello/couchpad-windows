using System.Buffers.Binary;
using Microsoft.Win32;

namespace CouchPad.Platform.Windows;

/// <summary>
/// Reads the raw EDID block of a display target from the Windows registry.
/// Read-only and fail-safe: non-Windows, missing keys, malformed values or any
/// other error return <see langword="null"/>, so callers fall back to
/// "Automatic" instead of guessing a mode.
/// </summary>
public static class EdidReader
{
    private const string DisplayEnumKey = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";
    private const string DeviceParametersKey = "Device Parameters";
    private const string EdidValueName = "EDID";
    private const int MinimumEdidLength = 128;

    private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    /// <summary>
    /// Returns the EDID bytes whose manufacturer/product match
    /// <paramref name="options"/>, preferring the registry monitor id contained
    /// in the target's CCD device path.
    /// </summary>
    public static byte[]? ReadForDevice(WindowsDisplayOptions options)
    {
        if (options is null || !OperatingSystem.IsWindows())
            return null;

        try
        {
            using var displays = Registry.LocalMachine.OpenSubKey(DisplayEnumKey, writable: false);
            if (displays is null)
                return null;

            byte[]? fallback = null;
            foreach (var monitorId in displays.GetSubKeyNames())
            {
                var preferred = ContainsMonitorId(options.DevicePath, monitorId);
                using var monitor = displays.OpenSubKey(monitorId, writable: false);
                if (monitor is null)
                    continue;

                foreach (var instanceId in monitor.GetSubKeyNames())
                {
                    using var instance = monitor.OpenSubKey(instanceId, writable: false);
                    using var parameters = instance?.OpenSubKey(DeviceParametersKey, writable: false);
                    if (parameters?.GetValue(EdidValueName) is not byte[] edid ||
                        !IsValid(edid) ||
                        !MatchesIdentity(edid, options))
                        continue;

                    if (preferred)
                        return edid;

                    fallback ??= edid;
                }
            }

            return fallback;
        }
        catch
        {
            return null;
        }
    }

    private static bool ContainsMonitorId(string? devicePath, string monitorId) =>
        !string.IsNullOrEmpty(devicePath) &&
        !string.IsNullOrEmpty(monitorId) &&
        devicePath.Contains(monitorId, StringComparison.OrdinalIgnoreCase);

    private static bool IsValid(byte[] edid) =>
        edid.Length >= MinimumEdidLength && edid.AsSpan(0, Header.Length).SequenceEqual(Header);

    private static bool MatchesIdentity(ReadOnlySpan<byte> edid, WindowsDisplayOptions options)
    {
        // EDID stores the manufacturer word big-endian, but the CCD target name
        // reports it byte-swapped. Accept both so options captured from CCD still
        // match the registry block.
        var manufacturer = BinaryPrimitives.ReadUInt16BigEndian(edid.Slice(8, 2));
        var swappedManufacturer = (ushort)((manufacturer >> 8) | (manufacturer << 8));
        if (manufacturer != options.EdidManufacturer && swappedManufacturer != options.EdidManufacturer)
            return false;

        var product = BinaryPrimitives.ReadUInt16LittleEndian(edid.Slice(10, 2));
        return product == options.EdidProduct;
    }
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using CouchPad.Core.Display;

namespace CouchPad.Platform.Windows;

/// <summary>Leitura CCD. Escrita permanece indisponível até implementação e validação em hardware.</summary>
public sealed class WindowsDisplayService : IDisplayService
{
    private const uint ActivePaths = 2;
    public const uint AllPaths = 1;
    private const int PathSize = 72, ModeSize = 64, InsufficientBuffer = 122;

    private readonly WindowsDisplayOptions _options;

    public WindowsDisplayService(WindowsDisplayOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));

    public DisplayState GetCurrent()
    {
        var all = Capture(AllPaths);
        var target = SelectTarget(all.Targets, _options.DevicePath, _options.EdidManufacturer, _options.EdidProduct, _options.ExactName);
        return MapState(Capture(ActivePaths), target);
    }

    public static DisplayState MapState(Snapshot snapshot, Target tv)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.QueryFlags != ActivePaths || snapshot.PathSize != PathSize || snapshot.ModeSize != ModeSize)
            throw new InvalidOperationException("Unsupported display snapshot.");
        var paths = Convert.FromBase64String(snapshot.PathsBase64);
        var modes = Convert.FromBase64String(snapshot.ModesBase64);
        if (paths.Length == 0 || paths.Length % PathSize != 0 || modes.Length % ModeSize != 0)
            throw new InvalidOperationException("Invalid display snapshot.");
        var active = new List<(uint Low, int High, uint Source, bool Tv)>();
        for (var offset = 0; offset < paths.Length; offset += PathSize)
        {
            var isTv = BitConverter.ToUInt32(paths, offset + 20) == tv.AdapterLow && BitConverter.ToInt32(paths, offset + 24) == tv.AdapterHigh && BitConverter.ToUInt32(paths, offset + 28) == tv.Id;
            active.Add((BitConverter.ToUInt32(paths, offset), BitConverter.ToInt32(paths, offset + 4), BitConverter.ToUInt32(paths, offset + 8), isTv));
        }
        var tvPaths = active.Where(x => x.Tv).ToArray();
        var primaryPaths = active.Where(x => !x.Tv).ToArray();
        if (tvPaths.Length > 1 || primaryPaths.Length > 1 || (tvPaths.Length == 0 && primaryPaths.Length == 0))
            throw new InvalidOperationException("Unsupported or ambiguous active display topology.");
        var mode = tvPaths.Length == 0 ? DisplayMode.InternalOnly : primaryPaths.Length == 0 ? DisplayMode.TvOnly :
            (tvPaths[0].Low, tvPaths[0].High, tvPaths[0].Source) == (primaryPaths[0].Low, primaryPaths[0].High, primaryPaths[0].Source) ? DisplayMode.Mirror : DisplayMode.Extend;
        return new DisplayState(mode, primaryPaths.Length == 1, tvPaths.Length == 1) { Topology = JsonSerializer.Serialize(snapshot) };
    }

    public Snapshot Capture(uint queryFlags)
    {
        if (queryFlags is not (ActivePaths or AllPaths)) throw new ArgumentOutOfRangeException(nameof(queryFlags));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Check(GetDisplayConfigBufferSizes(queryFlags, out var pathCount, out var modeCount));
            var paths = new byte[checked((int)pathCount * PathSize)];
            var modes = new byte[checked((int)modeCount * ModeSize)];
            var p = GCHandle.Alloc(paths, GCHandleType.Pinned);
            var m = GCHandle.Alloc(modes, GCHandleType.Pinned);
            try
            {
                var result = QueryDisplayConfig(queryFlags, ref pathCount, p.AddrOfPinnedObject(), ref modeCount, m.AddrOfPinnedObject(), IntPtr.Zero);
                if (result == InsufficientBuffer) continue;
                Check(result);
                Array.Resize(ref paths, checked((int)pathCount * PathSize));
                Array.Resize(ref modes, checked((int)modeCount * ModeSize));
                var targets = new List<Target>();
                for (var i = 0; i < pathCount; i++)
                {
                    int offset = checked((int)i * PathSize);
                    var info = new TargetName { Header = new Header { Type = 2, Size = (uint)Marshal.SizeOf<TargetName>(), AdapterLow = BitConverter.ToUInt32(paths, offset + 20), AdapterHigh = BitConverter.ToInt32(paths, offset + 24), Id = BitConverter.ToUInt32(paths, offset + 28) }, FriendlyName = "", DevicePath = "" };
                    var status = DisplayConfigGetDeviceInfo(ref info);
                    targets.Add(new Target(info.Header.AdapterLow, info.Header.AdapterHigh, info.Header.Id, status == 0 ? info.DevicePath : null, status == 0 ? info.FriendlyName : null, status == 0 && (info.Flags & 4) != 0 ? info.Manufacturer : null, status == 0 && (info.Flags & 4) != 0 ? info.Product : null, status));
                }
                return new Snapshot(1, queryFlags, PathSize, ModeSize, Convert.ToBase64String(paths), Convert.ToBase64String(modes), targets);
            }
            finally { p.Free(); m.Free(); }
        }
        throw new InvalidOperationException("Display configuration changed repeatedly during capture.");
    }

    public static Target SelectTarget(IEnumerable<Target> targets, string devicePath, ushort manufacturer, ushort product, string? exactName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(devicePath);
        var unique = targets.Where(t => t.Status == 0).DistinctBy(t => (t.AdapterLow, t.AdapterHigh, t.Id)).ToArray();
        var matches = unique.Where(t => string.Equals(t.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase) && t.EdidManufacturer == manufacturer && t.EdidProduct == product).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1) throw new InvalidOperationException("Ambiguous display identity.");
        matches = unique.Where(t => exactName is not null && string.Equals(t.FriendlyName, exactName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(t.DevicePath) || t.EdidManufacturer is null || t.EdidProduct is null)
            && (string.IsNullOrEmpty(t.DevicePath) || string.Equals(t.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase))
            && (t.EdidManufacturer is null || t.EdidManufacturer == manufacturer)
            && (t.EdidProduct is null || t.EdidProduct == product)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("Display identity missing or ambiguous.");
    }

    public Task<DisplayState> ApplyAsync(DisplayMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException(_options.AllowWrites ? "Display writes are not implemented." : "Display writes are disabled.");
    public Task RestoreAsync(DisplayState state, CancellationToken cancellationToken = default) => throw new NotSupportedException(_options.AllowWrites ? "Display writes are not implemented." : "Display writes are disabled.");
    private static void Check(int error) { if (error != 0) throw new Win32Exception(error); }
    public sealed record Target(uint AdapterLow, int AdapterHigh, uint Id, string? DevicePath, string? FriendlyName, ushort? EdidManufacturer, ushort? EdidProduct, int Status);
    public sealed record Snapshot(int SchemaVersion, uint QueryFlags, int PathSize, int ModeSize, string PathsBase64, string ModesBase64, List<Target> Targets);
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size, AdapterLow; public int AdapterHigh; public uint Id; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TargetName { public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product; public uint Connector; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath; }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, IntPtr pathInfo, ref uint modes, IntPtr modeInfo, IntPtr topology);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref TargetName request);
}

public sealed record WindowsDisplayOptions(string DevicePath, ushort EdidManufacturer, ushort EdidProduct, string? ExactName = null, bool AllowWrites = false);

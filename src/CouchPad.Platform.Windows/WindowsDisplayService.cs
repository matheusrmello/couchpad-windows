using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using CouchPad.Core.Display;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Serviço CCD; escrita opt-in, ainda requer validação em hardware.</summary>
public sealed class WindowsDisplayService : IDisplayService
{
    private const uint ActivePaths = 2;
    public const uint AllPaths = 1;
    private const int PathSize = 72, ModeSize = 64, InsufficientBuffer = 122;

    private readonly WindowsDisplayOptions _options;
    private readonly RescueTransactionContext? _transaction;
    private readonly bool _recoveryWriter;
    private readonly Func<uint, Snapshot>? _captureOverride;
    private readonly Func<byte[], byte[], uint, int>? _setOverride;

    public WindowsDisplayService(WindowsDisplayOptions options, RescueTransactionContext? transaction = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transaction = transaction;
    }

    private WindowsDisplayService(WindowsDisplayOptions options, RescueTransactionContext transaction, bool recoveryWriter)
        : this(options, transaction) => _recoveryWriter = recoveryWriter;

    internal static WindowsDisplayService ForRecovery(WindowsDisplayOptions options, RescueTransactionContext transaction) =>
        new(options, transaction, recoveryWriter: true);

    internal WindowsDisplayService(WindowsDisplayOptions options, Func<uint, Snapshot> capture, Func<byte[], byte[], uint, int> set,
        RescueTransactionContext? transaction = null) : this(options, transaction)
    {
        _captureOverride = capture ?? throw new ArgumentNullException(nameof(capture));
        _setOverride = set ?? throw new ArgumentNullException(nameof(set));
    }

    public DisplayState GetCurrent()
    {
        var all = Capture(AllPaths);
        var target = SelectTarget(all.Targets, _options.DevicePath, _options.EdidManufacturer, _options.EdidProduct, _options.ExactName);
        var selector = _options.InternalDisplay ?? throw new InvalidOperationException("Internal display selector is required.");
        var internalDisplay = SelectTarget(all.Targets, selector.DevicePath, selector.EdidManufacturer, selector.EdidProduct, selector.ExactName);
        return MapState(Capture(ActivePaths), target, internalDisplay);
    }

    public static DisplayState MapState(Snapshot snapshot, Target tv, Target? internalDisplay = null)
    {
        ArgumentNullException.ThrowIfNull(tv);
        if (internalDisplay is not null &&
            (tv.AdapterLow, tv.AdapterHigh, tv.Id) == (internalDisplay.AdapterLow, internalDisplay.AdapterHigh, internalDisplay.Id))
            throw new InvalidOperationException("Display identities must differ.");
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
            var isInternal = internalDisplay is not null && BitConverter.ToUInt32(paths, offset + 20) == internalDisplay.AdapterLow &&
                BitConverter.ToInt32(paths, offset + 24) == internalDisplay.AdapterHigh && BitConverter.ToUInt32(paths, offset + 28) == internalDisplay.Id;
            if (internalDisplay is not null && !isTv && !isInternal)
                throw new InvalidOperationException("Unexpected active display.");
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
        if (_captureOverride is not null) return _captureOverride(queryFlags);
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

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public Task<DisplayState> ApplyAsync(DisplayMode mode, CancellationToken cancellationToken = default)
    {
        if (!_options.AllowWrites) throw new NotSupportedException("Display writes are disabled.");
        if (_setOverride is null && _transaction is null) throw new RescueDecisionUncertainException("A guarded transaction is required.");
        return ApplyCoreAsync(mode, cancellationToken);
    }

    private async Task<DisplayState> ApplyCoreAsync(DisplayMode mode, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var mutation = _transaction is not null
                ? await DisplayMutationLock.AcquireAsync(_transaction.MutationLockPath, cancellationToken).ConfigureAwait(false)
                : null;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureMutationAllowed();
            var all = Capture(AllPaths);
            var tv = SelectTarget(all.Targets, _options.DevicePath, _options.EdidManufacturer, _options.EdidProduct, _options.ExactName);
            var before = Capture(ActivePaths);
            var selector = _options.InternalDisplay ?? throw new InvalidOperationException("Internal display selector is required.");
            var internalDisplay = SelectTarget(all.Targets, selector.DevicePath, selector.EdidManufacturer, selector.EdidProduct, selector.ExactName);
            var proposal = DisplayTopologyPlanner.Create(before, all, tv, internalDisplay, mode);
            DisplayState? after = null;
            DisplayTransaction.Execute(
                () => SetConfiguration(proposal.Paths, Array.Empty<byte>(), 0x40),
                () => { SetConfiguration(proposal.Paths, Array.Empty<byte>(), 0x80); EnsureMutationAllowed(); },
                () => { after = GetCurrent(); if (after.Mode != mode) throw new InvalidOperationException("Display verification failed."); EnsureMutationAllowed(); },
                () => RestoreSnapshot(before), cancellationToken);
            return after!;
        }
        finally { _writeGate.Release(); }
    }

    public Task RestoreAsync(DisplayState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_options.AllowWrites) throw new NotSupportedException("Display writes are disabled.");
        if (_setOverride is null && _transaction is null) throw new RescueDecisionUncertainException("A guarded transaction is required.");
        var target = JsonSerializer.Deserialize<Snapshot>(state.Topology ?? throw new InvalidOperationException("Missing display topology."))
            ?? throw new InvalidOperationException("Invalid display topology.");
        return RestoreCoreAsync(target, cancellationToken);
    }

    private async Task RestoreCoreAsync(Snapshot target, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var mutation = _transaction is not null
                ? await DisplayMutationLock.AcquireAsync(_transaction.MutationLockPath, cancellationToken).ConfigureAwait(false)
                : null;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureMutationAllowed();
            var before = Capture(ActivePaths);
            var (paths, modes) = DecodeSnapshot(target);
            DisplayTransaction.Execute(
                () => SetConfiguration(paths, modes, 0x40),
                () => { SetConfiguration(paths, modes, 0x80); EnsureMutationAllowed(); },
                () => { VerifySnapshot(target); EnsureMutationAllowed(); },
                () => RestoreSnapshot(before), cancellationToken);
        }
        finally { _writeGate.Release(); }
    }

    private void RestoreSnapshot(Snapshot snapshot)
    {
        var (paths, modes) = DecodeSnapshot(snapshot);
        SetConfiguration(paths, modes, 0x40);
        SetConfiguration(paths, modes, 0x80);
        VerifySnapshot(snapshot);
    }

    private void VerifySnapshot(Snapshot expected)
    {
        var actual = Capture(ActivePaths);
        if (!DisplaySnapshotComparer.SemanticallyEqual(expected, actual))
            throw new InvalidOperationException("Restored display configuration could not be verified.");
    }

    private static (byte[] Paths, byte[] Modes) DecodeSnapshot(Snapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.QueryFlags != ActivePaths || snapshot.PathSize != PathSize || snapshot.ModeSize != ModeSize)
            throw new InvalidOperationException("Unsupported display snapshot.");
        var paths = Convert.FromBase64String(snapshot.PathsBase64);
        var modes = Convert.FromBase64String(snapshot.ModesBase64);
        if (paths.Length == 0 || paths.Length % PathSize != 0 || modes.Length == 0 || modes.Length % ModeSize != 0)
            throw new InvalidOperationException("Invalid display snapshot.");
        return (paths, modes);
    }

    private void SetConfiguration(byte[] paths, byte[] modes, uint action)
    {
        if (action == 0x80) EnsureMutationAllowed();
        if (_setOverride is not null) { Check(_setOverride(paths, modes, action)); return; }
        var pathPin = GCHandle.Alloc(paths, GCHandleType.Pinned);
        var modePin = GCHandle.Alloc(modes, GCHandleType.Pinned);
        try { Check(SetDisplayConfig((uint)(paths.Length / PathSize), pathPin.AddrOfPinnedObject(), (uint)(modes.Length / ModeSize), modes.Length == 0 ? IntPtr.Zero : modePin.AddrOfPinnedObject(), action | 0x20)); }
        finally { pathPin.Free(); modePin.Free(); }
    }

    private void EnsureMutationAllowed()
    {
        if (_setOverride is not null && _transaction is null) return;
        if (_transaction is null) throw new RescueDecisionUncertainException("A guarded transaction is required.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            _transaction.RequireAsync(_recoveryWriter ? RescueDecisionState.RecoveryRequested : RescueDecisionState.Armed,
                deadline.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException ex)
        {
            throw new RescueDecisionUncertainException("Rescue decision could not be verified in time.", ex);
        }
    }

    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(uint pathCount, IntPtr paths, uint modeCount, IntPtr modes, uint flags);
    private static void Check(int error) { if (error != 0) throw new Win32Exception(error); }
    public sealed record Target(uint AdapterLow, int AdapterHigh, uint Id, string? DevicePath, string? FriendlyName, ushort? EdidManufacturer, ushort? EdidProduct, int Status);
    public sealed record Snapshot(int SchemaVersion, uint QueryFlags, int PathSize, int ModeSize, string PathsBase64, string ModesBase64, List<Target> Targets);
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size, AdapterLow; public int AdapterHigh; public uint Id; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TargetName { public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product; public uint Connector; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath; }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, IntPtr pathInfo, ref uint modes, IntPtr modeInfo, IntPtr topology);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref TargetName request);
}

public sealed record WindowsDisplayOptions(string DevicePath, ushort EdidManufacturer, ushort EdidProduct, string? ExactName = null, bool AllowWrites = false, DisplaySelector? InternalDisplay = null);
public sealed record DisplaySelector(string DevicePath, ushort EdidManufacturer, ushort EdidProduct, string? ExactName = null);

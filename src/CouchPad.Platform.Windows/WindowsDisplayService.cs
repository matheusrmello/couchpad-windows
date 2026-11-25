using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using CouchPad.Core.Display;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Serviço CCD; escrita opt-in, ainda requer validação em hardware.</summary>
public sealed class WindowsDisplayService : IDisplayService, IDisplayWriteControl
{
    private const uint ActivePaths = 2;
    public const uint AllPaths = 1;
    private const int PathSize = 72, ModeSize = 64, InsufficientBuffer = 122;
    private const int DevModeSize = 220;
    private const uint EnumCurrentSettings = 0xFFFFFFFF;
    private const uint CdsUpdateRegistry = 0x1;
    private const int DispChangeSuccessful = 0;
    private const int DispChangeBadMode = -2;
    private const int BadModeRetries = 2;
    private const int BadModeRetryDelayMs = 250;
    private const long DisplayReadyTimeoutMs = 10_000;
    private const uint DisplayDeviceAttachedToDesktop = 0x1;
    private const uint DmPelsWidth = 0x80000, DmPelsHeight = 0x100000, DmDisplayFrequency = 0x400000;

    internal static int DevModeSizeBytes => Marshal.SizeOf<DevMode>();
    internal static int DisplayDeviceSizeBytes => Marshal.SizeOf<DisplayDevice>();

    private readonly WindowsDisplayOptions _options;
    private readonly RescueTransactionContext? _transaction;
    private readonly bool _recoveryWriter;
    private readonly CancellationTokenSource _writesRevoked = new();
    private readonly Func<uint, Snapshot>? _captureOverride;
    private readonly Func<byte[], byte[], uint, int>? _setOverride;
    private readonly Func<string, byte[]?>? _edidOverride;
    private readonly Lazy<IReadOnlyList<DisplayModeOption>> _edidModes;

    public WindowsDisplayService(WindowsDisplayOptions options, RescueTransactionContext? transaction = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transaction = transaction;
        _edidModes = new Lazy<IReadOnlyList<DisplayModeOption>>(LoadEdidModes);
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

    internal WindowsDisplayService(WindowsDisplayOptions options, Func<string, byte[]?>? edidOverride) : this(options) =>
        _edidOverride = edidOverride;

    /// <summary>Test seam: display writes plus a fixed EDID, still with no real hardware.</summary>
    internal WindowsDisplayService(WindowsDisplayOptions options, Func<uint, Snapshot> capture, Func<byte[], byte[], uint, int> set,
        Func<string, byte[]?>? edidOverride) : this(options, capture, set) =>
        _edidOverride = edidOverride;

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
            Check(GetDisplayConfigBufferSizes(queryFlags, out var pathCount, out var modeCount), "GetDisplayConfigBufferSizes");
            var paths = new byte[checked((int)pathCount * PathSize)];
            var modes = new byte[checked((int)modeCount * ModeSize)];
            var p = GCHandle.Alloc(paths, GCHandleType.Pinned);
            var m = GCHandle.Alloc(modes, GCHandleType.Pinned);
            try
            {
                var result = QueryDisplayConfig(queryFlags, ref pathCount, p.AddrOfPinnedObject(), ref modeCount, m.AddrOfPinnedObject(), IntPtr.Zero);
                if (result == InsufficientBuffer) continue;
                Check(result, "QueryDisplayConfig");
                Array.Resize(ref paths, checked((int)pathCount * PathSize));
                Array.Resize(ref modes, checked((int)modeCount * ModeSize));
                var targets = new List<Target>();
                for (var i = 0; i < pathCount; i++)
                {
                    int offset = checked((int)i * PathSize);
                    var info = new TargetName { Header = new Header { Type = 2, Size = (uint)Marshal.SizeOf<TargetName>(), AdapterLow = BitConverter.ToUInt32(paths, offset + 20), AdapterHigh = BitConverter.ToInt32(paths, offset + 24), Id = BitConverter.ToUInt32(paths, offset + 28) }, FriendlyName = "", DevicePath = "" };
                    var status = DisplayConfigGetDeviceInfo(ref info);
                    targets.Add(new Target(info.Header.AdapterLow, info.Header.AdapterHigh, info.Header.Id, status == 0 ? info.DevicePath : null, status == 0 ? info.FriendlyName : null, status == 0 && (info.Flags & 4) != 0 ? info.Manufacturer : null, status == 0 && (info.Flags & 4) != 0 ? info.Product : null, status)
                    {
                        IsActive = (BitConverter.ToUInt32(paths, offset + 68) & 1) != 0,
                        IsAvailable = BitConverter.ToUInt32(paths, offset + 60) != 0,
                        IsHdmi = BitConverter.ToUInt32(paths, offset + 36) == 5,
                    });
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

    public IReadOnlyList<DisplayModeOption> GetAvailableModes() => MergeModes(_edidModes.Value, EnumerateWindowsModes());

    /// <summary>Somente o que o EDID anuncia; vazio quando o EDID não está disponível.</summary>
    public IReadOnlyList<DisplayModeOption> GetTvAdvertisedModes() => _edidModes.Value;

    private IReadOnlyList<DisplayModeOption> LoadEdidModes()
    {
        try
        {
            var edid = _edidOverride is not null ? _edidOverride(_options.DevicePath) : EdidReader.ReadForDevice(_options);
            return edid is null ? Array.Empty<DisplayModeOption>() : EdidModeParser.Parse(edid);
        }
        catch
        {
            return Array.Empty<DisplayModeOption>();
        }
    }

    /// <summary>
    /// Merges the modes advertised by the EDID with the modes Windows exposes for
    /// the TV, dropping duplicates and sorting by pixel area then refresh rate
    /// descending. Pure and deterministic.
    /// </summary>
    internal static IReadOnlyList<DisplayModeOption> MergeModes(
        IReadOnlyList<DisplayModeOption> edid,
        IReadOnlyList<DisplayModeOption> windows)
    {
        ArgumentNullException.ThrowIfNull(edid);
        ArgumentNullException.ThrowIfNull(windows);
        var seen = new HashSet<(int Width, int Height, int RefreshHz)>();
        var merged = new List<DisplayModeOption>(edid.Count + windows.Count);
        foreach (var mode in edid.Concat(windows))
        {
            if (mode.Width <= 0 || mode.Height <= 0 || mode.RefreshHz <= 0) continue;
            if (seen.Add((mode.Width, mode.Height, mode.RefreshHz))) merged.Add(mode);
        }
        return merged
            .OrderByDescending(mode => (long)mode.Width * mode.Height)
            .ThenByDescending(mode => mode.RefreshHz)
            .ThenByDescending(mode => mode.Width)
            .ToArray();
    }

    /// <summary>Reads every 32-bit mode Windows exposes for the TV; empty when the TV has no GDI device.</summary>
    private IReadOnlyList<DisplayModeOption> EnumerateWindowsModes()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<DisplayModeOption>();
        string? deviceName;
        try
        {
            deviceName = TryResolveGdiDeviceName();
        }
        catch
        {
            return Array.Empty<DisplayModeOption>();
        }
        if (string.IsNullOrEmpty(deviceName)) return Array.Empty<DisplayModeOption>();

        var modes = new List<DisplayModeOption>();
        for (uint index = 0; index < 512; index++)
        {
            var candidate = CreateDevMode();
            if (!EnumDisplaySettingsW(deviceName, index, ref candidate)) break;
            if (candidate.dmBitsPerPel != 32 ||
                candidate.dmPelsWidth == 0 ||
                candidate.dmPelsHeight == 0 ||
                candidate.dmDisplayFrequency == 0)
                continue;
            modes.Add(new DisplayModeOption((int)candidate.dmPelsWidth, (int)candidate.dmPelsHeight, (int)candidate.dmDisplayFrequency));
        }
        return modes;
    }

    public Task<DisplayState> ApplyAsync(DisplayMode mode, DisplayPreference? preference = null, CancellationToken cancellationToken = default)
    {
        if (mode == DisplayMode.Mirror)
        {
            var error = new NotSupportedException("Applying mirrored displays is disabled.");
            DisplayApplyFailure.MarkUnchanged(error);
            throw error;
        }
        if (!_options.AllowWrites || _writesRevoked.IsCancellationRequested)
        {
            var error = new NotSupportedException("Display writes are disabled for this session.");
            DisplayApplyFailure.MarkUnchanged(error);
            throw error;
        }
        if (_setOverride is null && _transaction is null) throw new RescueDecisionUncertainException("A guarded transaction is required.");
        return ApplyWithRevocationAsync(mode, preference, cancellationToken);
    }

    public bool CanApply => _options.AllowWrites && !_writesRevoked.IsCancellationRequested;

    public void DisableNewWrites() => _writesRevoked.Cancel();

    private async Task<DisplayState> ApplyWithRevocationAsync(DisplayMode mode, DisplayPreference? preference, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _writesRevoked.Token);
        var attempted = false;
        try { return await ApplyCoreAsync(mode, preference, linked.Token, () => attempted = true).ConfigureAwait(false); }
        catch (Exception ex) when (!attempted && ex is not RescueDecisionUncertainException)
        {
            DisplayApplyFailure.MarkUnchanged(ex);
            throw;
        }
    }

    private async Task<DisplayState> ApplyCoreAsync(DisplayMode mode, DisplayPreference? preference, CancellationToken cancellationToken, Action markAttempted)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var mutation = _transaction is not null
                ? await DisplayMutationLock.AcquireAsync(_transaction.MutationLockPath, cancellationToken).ConfigureAwait(false)
                : null;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureMutationAllowed();
            var normalized = (preference ?? DisplayPreference.Auto).Normalize();
            var all = Capture(AllPaths);
            var tv = SelectTarget(all.Targets, _options.DevicePath, _options.EdidManufacturer, _options.EdidProduct, _options.ExactName);
            var before = Capture(ActivePaths);
            var selector = _options.InternalDisplay ?? throw new InvalidOperationException("Internal display selector is required.");
            var internalDisplay = SelectTarget(all.Targets, selector.DevicePath, selector.EdidManufacturer, selector.EdidProduct, selector.ExactName);
            var proposal = DisplayTopologyPlanner.Create(before, all, tv, internalDisplay, mode);
            var requestedMode = ResolveRequestedMode(normalized);
            // The Windows driver can list modes the sink does not accept over the
            // current link (for example 4K60 on an HDMI 1.4 EDID). Applying one of
            // those ends in DISP_CHANGE_BADMODE, so trust the EDID list when it is
            // available and refuse before any mutation.
            if (requestedMode is not null && !IsModeAdvertised(requestedMode, _edidModes.Value))
                throw DisplayApplyException.ForMode(
                    DisplayApplyErrorCode.UnsupportedMode,
                    requestedMode,
                    $"The TV EDID does not advertise {requestedMode.Width}x{requestedMode.Height}@{requestedMode.RefreshHz} Hz.");
            // Resolve the per-monitor DPI key before touching the display. Windows
            // only creates that profile after the TV has been configured once, so
            // the scale is best-effort: a missing profile or EDID skip only the
            // scale and never block the display/audio activation.
            var scalePercent = normalized.ScalePercent;
            string? dpiKey = null;
            int? previousDpi = null;
            if (scalePercent is not null)
            {
                try
                {
                    var edid = _edidOverride?.Invoke(_options.DevicePath) ?? EdidReader.ReadForDevice(_options);
                    var prefix = EdidIdentity.BuildDpiPrefix(edid);
                    dpiKey = prefix is null ? null : PerMonitorDpi.FindKey(prefix);
                    previousDpi = dpiKey is null ? null : PerMonitorDpi.ReadDpiValue(dpiKey);
                }
                catch
                {
                    dpiKey = null;
                    previousDpi = null;
                }
            }

            string? gdiDeviceName = null;
            DisplayState? after = null;
            DisplayTransaction.Execute(
                () => SetConfiguration(proposal.Paths, Array.Empty<byte>(), 0x40),
                () =>
                {
                    markAttempted();
                    SetConfiguration(proposal.Paths, Array.Empty<byte>(), 0x80);
                    EnsureMutationAllowed();
                    if (requestedMode is not null) gdiDeviceName = ApplyDisplayMode(requestedMode);
                    if (scalePercent is { } scale && dpiKey is not null && !PerMonitorDpi.Apply(dpiKey, scale))
                        throw new DisplayApplyException(DisplayApplyErrorCode.ScaleFailed, "Failed to apply the TV display scale.");
                },
                () =>
                {
                    after = GetCurrent();
                    if (after.Mode != mode)
                        throw new DisplayApplyException(DisplayApplyErrorCode.TopologyVerificationFailed, "Display verification failed.");
                    if (requestedMode is not null) VerifyDisplayMode(gdiDeviceName!, requestedMode);
                    if (scalePercent is { } expectedScale && dpiKey is not null && PerMonitorDpi.ReadPercent(dpiKey) != expectedScale)
                        throw new DisplayApplyException(DisplayApplyErrorCode.ScaleFailed, "TV display scale verification failed.");
                    EnsureMutationAllowed();
                },
                () =>
                {
                    RestoreSnapshot(before);
                    if (previousDpi is { } value && dpiKey is not null) PerMonitorDpi.WriteDpiValue(dpiKey, value);
                },
                cancellationToken);
            return after!;
        }
        finally { _writeGate.Release(); }
    }

    private static DisplayModeOption? ResolveRequestedMode(DisplayPreference preference) =>
        preference.Width is { } width && preference.Height is { } height && preference.RefreshHz is { } refresh
            ? new DisplayModeOption(width, height, refresh)
            : null;

    public Task RestoreAsync(DisplayState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!_options.AllowWrites) throw new NotSupportedException("Display writes are disabled.");
        if (_setOverride is null && _transaction is null) throw new RescueDecisionUncertainException("A guarded transaction is required.");
        var target = JsonSerializer.Deserialize<Snapshot>(state.Topology ?? throw new InvalidOperationException("Missing display topology."))
            ?? throw new InvalidOperationException("Invalid display topology.");
        return RestoreCoreAsync(target, cancellationToken);
    }

    private async Task RestoreCoreAsync(Snapshot expected, CancellationToken cancellationToken)
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
            // Adapter LUIDs and source ids can change across reboots or driver
            // resets. Rebind the journal snapshot to the current identities
            // before validating or applying anything.
            var target = DisplaySnapshotRehydrator.Rehydrate(expected, Capture(AllPaths));
            if (DisplaySnapshotComparer.SemanticallyEqual(target, before)) return;
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
        if (action == 0x80 && _transaction is not null)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _transaction.ExecuteIfStateAsync(_recoveryWriter ? RescueDecisionState.RecoveryRequested : RescueDecisionState.Armed,
                () => SetConfigurationCore(paths, modes, action), deadline.Token).GetAwaiter().GetResult();
            return;
        }
        if (action == 0x80) EnsureMutationAllowed();
        SetConfigurationCore(paths, modes, action);
    }

    private void SetConfigurationCore(byte[] paths, byte[] modes, uint action)
    {
        var operation = action == 0x40 ? "SetDisplayConfig.Validate" : "SetDisplayConfig.Apply";
        if (_setOverride is not null) { Check(_setOverride(paths, modes, action), operation); return; }
        var pathPin = GCHandle.Alloc(paths, GCHandleType.Pinned);
        var modePin = GCHandle.Alloc(modes, GCHandleType.Pinned);
        try { Check(SetDisplayConfig((uint)(paths.Length / PathSize), pathPin.AddrOfPinnedObject(), (uint)(modes.Length / ModeSize), modes.Length == 0 ? IntPtr.Zero : modePin.AddrOfPinnedObject(), action | 0x20), operation); }
        finally { pathPin.Free(); modePin.Free(); }
    }

    /// <summary>
    /// True when the requested mode appears in the EDID list. An empty list means
    /// the EDID is unavailable, so the gate stays open and the Windows list rules.
    /// </summary>
    internal static bool IsModeAdvertised(DisplayModeOption requested, IReadOnlyList<DisplayModeOption> advertised) =>
        advertised.Count == 0 ||
        advertised.Any(mode => mode.Width == requested.Width &&
            mode.Height == requested.Height &&
            RefreshMatches((uint)mode.RefreshHz, requested.RefreshHz));

    private string ApplyDisplayMode(DisplayModeOption mode)
    {
        var deviceName = WaitForDisplayReady(TryResolveAttachedGdiDeviceName, mode);
        var devMode = FindDisplayMode(deviceName, mode) ?? throw DisplayApplyException.ForMode(
            DisplayApplyErrorCode.UnsupportedMode,
            mode,
            $"Requested TV display mode {mode.Width}x{mode.Height}@{mode.RefreshHz} Hz is unavailable.");

        devMode.dmFields |= DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
        var result = ChangeDisplaySettingsExW(deviceName, ref devMode, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
        for (var retry = 0; result == DispChangeBadMode && retry < BadModeRetries; retry++)
        {
            // A just-attached TV can answer DISP_CHANGE_BADMODE while Windows is
            // still publishing its modes, so re-resolve the device and retry.
            Thread.Sleep(BadModeRetryDelayMs);
            deviceName = TryResolveAttachedGdiDeviceName() ?? throw DisplayApplyException.ForMode(
                DisplayApplyErrorCode.DisplayNotReady, mode, "TV display is no longer attached to the desktop.", result);
            result = ChangeDisplaySettingsExW(deviceName, ref devMode, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
        }
        if (result != DispChangeSuccessful)
            throw DisplayApplyException.ForMode(
                DisplayApplyErrorCode.ModeRejected, mode, $"Changing the TV display mode failed ({result}).", result);

        return deviceName;
    }

    private DevMode? FindDisplayMode(string deviceName, DisplayModeOption mode)
    {
        for (uint index = 0; ; index++)
        {
            var candidate = CreateDevMode();
            if (!EnumDisplaySettingsW(deviceName, index, ref candidate)) return null;
            if (candidate.dmPelsWidth == (uint)mode.Width &&
                candidate.dmPelsHeight == (uint)mode.Height &&
                RefreshMatches(candidate.dmDisplayFrequency, mode.RefreshHz))
                return candidate;
        }
    }

    private static void VerifyDisplayMode(string deviceName, DisplayModeOption mode)
    {
        var current = CreateDevMode();
        if (!EnumDisplaySettingsW(deviceName, EnumCurrentSettings, ref current) ||
            current.dmPelsWidth != (uint)mode.Width ||
            current.dmPelsHeight != (uint)mode.Height ||
            !RefreshMatches(current.dmDisplayFrequency, mode.RefreshHz))
            throw DisplayApplyException.ForMode(
                DisplayApplyErrorCode.ModeVerificationFailed, mode, "TV display mode verification failed.");
    }

    /// <summary>
    /// Waits up to 10 s for the TV to be attached to the desktop and answer with a
    /// usable current mode. The device is re-resolved on every poll because a TV
    /// can attach or re-enumerate after the CCD apply.
    /// </summary>
    private static string WaitForDisplayReady(Func<string?> resolveDevice, DisplayModeOption mode)
    {
        var deadline = Environment.TickCount64 + DisplayReadyTimeoutMs;
        while (true)
        {
            var deviceName = resolveDevice();
            if (!string.IsNullOrEmpty(deviceName))
            {
                var current = CreateDevMode();
                if (EnumDisplaySettingsW(deviceName, EnumCurrentSettings, ref current) &&
                    current.dmPelsWidth > 0 && current.dmPelsHeight > 0)
                    return deviceName;
            }
            if (Environment.TickCount64 >= deadline)
                throw DisplayApplyException.ForMode(
                    DisplayApplyErrorCode.DisplayNotReady, mode, "TV display did not become ready.");
            Thread.Sleep(50);
        }
    }

    // EDID timings round 59.94 Hz to 60, so an exact match is too strict.
    private static bool RefreshMatches(uint actual, int expected) => Math.Abs((long)actual - expected) <= 1;

    /// <summary>Read-only lookup used to enumerate modes; may fall back to a detached adapter.</summary>
    private string? TryResolveGdiDeviceName()
    {
        var monitorId = ExtractMonitorId(_options.DevicePath);
        string? fallback = null;
        for (uint adapterIndex = 0; ; adapterIndex++)
        {
            var adapter = CreateDisplayDevice();
            if (!EnumDisplayDevicesW(null, adapterIndex, ref adapter, 0)) break;
            if (!AdapterMatchesTvDevice(monitorId, adapter)) continue;
            if (IsAttachedToDesktop(adapter.StateFlags)) return adapter.DeviceName;
            fallback ??= adapter.DeviceName;
        }
        return fallback;
    }

    /// <summary>
    /// Resolver for display writes: only an adapter attached to the desktop is
    /// safe. A detached adapter answers DISP_CHANGE_BADMODE, so callers treat
    /// "none attached" as <see cref="DisplayApplyErrorCode.DisplayNotReady"/>.
    /// </summary>
    private string? TryResolveAttachedGdiDeviceName()
    {
        var monitorId = ExtractMonitorId(_options.DevicePath);
        for (uint adapterIndex = 0; ; adapterIndex++)
        {
            var adapter = CreateDisplayDevice();
            if (!EnumDisplayDevicesW(null, adapterIndex, ref adapter, 0)) return null;
            if (AdapterMatchesTvDevice(monitorId, adapter) && IsAttachedToDesktop(adapter.StateFlags))
                return adapter.DeviceName;
        }
    }

    private bool AdapterMatchesTvDevice(string? monitorId, DisplayDevice adapter)
    {
        if (IsTvDevice(monitorId, adapter.DeviceID, adapter.DeviceString)) return true;
        for (uint monitorIndex = 0; ; monitorIndex++)
        {
            var monitor = CreateDisplayDevice();
            if (!EnumDisplayDevicesW(adapter.DeviceName, monitorIndex, ref monitor, 0)) return false;
            if (IsTvDevice(monitorId, monitor.DeviceID, monitor.DeviceString)) return true;
        }
    }

    private static bool IsAttachedToDesktop(uint stateFlags) =>
        (stateFlags & DisplayDeviceAttachedToDesktop) != 0;

    private bool IsTvDevice(string? monitorId, string? deviceId, string? deviceString)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            if (!string.IsNullOrEmpty(_options.DevicePath) &&
                string.Equals(deviceId, _options.DevicePath, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(monitorId) &&
                deviceId.Contains(monitorId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return !string.IsNullOrEmpty(_options.ExactName) &&
            !string.IsNullOrEmpty(deviceString) &&
            string.Equals(deviceString, _options.ExactName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractMonitorId(string? devicePath)
    {
        const string marker = "DISPLAY#";
        if (string.IsNullOrEmpty(devicePath)) return null;
        var start = devicePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += marker.Length;
        var end = devicePath.IndexOf('#', start);
        return end > start ? devicePath[start..end] : null;
    }

    private static DevMode CreateDevMode() => new()
    {
        dmDeviceName = string.Empty,
        dmFormName = string.Empty,
        dmSize = (ushort)DevModeSize,
    };

    private static DisplayDevice CreateDisplayDevice() => new()
    {
        cb = Marshal.SizeOf<DisplayDevice>(),
        DeviceName = string.Empty,
        DeviceString = string.Empty,
        DeviceID = string.Empty,
        DeviceKey = string.Empty,
    };

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
    private static void Check(int error, string operation)
    {
        if (error == 0) return;
        var exception = new Win32Exception(error);
        exception.Data[RescueFailureLog.NativeOperationKey] = operation;
        throw exception;
    }
    public sealed record Target(uint AdapterLow, int AdapterHigh, uint Id, string? DevicePath, string? FriendlyName, ushort? EdidManufacturer, ushort? EdidProduct, int Status)
    {
        [System.Text.Json.Serialization.JsonIgnore] public bool IsActive { get; init; }
        [System.Text.Json.Serialization.JsonIgnore] public bool IsAvailable { get; init; }
        [System.Text.Json.Serialization.JsonIgnore] public bool IsHdmi { get; init; }
    }
    public sealed record Snapshot(int SchemaVersion, uint QueryFlags, int PathSize, int ModeSize, string PathsBase64, string ModesBase64, List<Target> Targets);
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size, AdapterLow; public int AdapterHigh; public uint Id; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TargetName { public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product; public uint Connector; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    // DEVMODEW: 64 name + 8 versions + 4 fields + 16 printer/position + 10 extras
    // + 64 form + 2 logpixels + 12 mode + 8 flags/frequency + 32 icm/panning = 220.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public DevModePosition Position;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct DevModePosition { public int X, Y; public uint Orientation, FixedOutput; }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, IntPtr pathInfo, ref uint modes, IntPtr modeInfo, IntPtr topology);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DisplayConfigGetDeviceInfo(ref TargetName request);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevicesW(string? deviceName, uint devNum, ref DisplayDevice displayDevice, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettingsW(string deviceName, uint modeNum, ref DevMode devMode);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsExW(string? deviceName, ref DevMode devMode, IntPtr hwnd, uint flags, IntPtr param);
}

public sealed record WindowsDisplayOptions(string DevicePath, ushort EdidManufacturer, ushort EdidProduct, string? ExactName = null, bool AllowWrites = false, DisplaySelector? InternalDisplay = null);
public sealed record DisplaySelector(string DevicePath, ushort EdidManufacturer, ushort EdidProduct, string? ExactName = null);

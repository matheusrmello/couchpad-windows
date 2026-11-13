using System.Diagnostics;
using System.Runtime.InteropServices;
using CouchPad.Core.Launchers;
using Microsoft.Win32;

namespace CouchPad.Platform.Windows;

/// <summary>Launches the selected game mode and tracks the session's window or client.</summary>
public sealed class WindowsLauncherService : ILauncherService
{
    private readonly IWindowsLauncherBackend _backend;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private OwnedLauncher? _owned;
    private HydraWindow? _hydraWindow;

    public WindowsLauncherService() : this(new WindowsLauncherBackend()) { }
    internal WindowsLauncherService(IWindowsLauncherBackend backend) => _backend = backend;

    public async Task StartAsync(LauncherSelection selection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hydraWindow is { } window && _backend.HydraWindows(window.Owner.Executable).Contains(window) ||
                _owned is not null && _backend.IsAlive(_owned)) return;
            _hydraWindow = null;
            _owned = null;
            await Task.Run(async () =>
            {
                var spec = _backend.Resolve(selection.Kind);
                cancellationToken.ThrowIfCancellationRequested();
                if (spec.Kind == LauncherKind.Hydra)
                {
                    var before = _backend.HydraWindows(spec.Executable);
                    _backend.Start(spec, false);
                    _hydraWindow = await _backend.FindNewHydraWindowAsync(spec, before, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var existingSteam = _backend.FindExistingSteam(spec.Executable);
                    cancellationToken.ThrowIfCancellationRequested();
                    _owned = existingSteam is null
                        ? _backend.Start(spec, true)
                        : RequestSteamBigPicture(spec, existingSteam);
                }
                if (cancellationToken.IsCancellationRequested && _owned is not null)
                {
                    try
                    {
                        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await _backend.CloseAsync(_owned, cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false);
                        _owned = null;
                    }
                    catch (Exception) { /* Keep the identity for a later close attempt. */ }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private OwnedLauncher RequestSteamBigPicture(LauncherSpec spec, OwnedLauncher steam)
    {
        _backend.Start(spec with { Argument = WindowsLauncherBackend.SteamOpenBigPictureArgument }, false);
        return steam;
    }

    public async Task CloseOwnedAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hydraWindow is { } window)
            {
                await _backend.CloseHydraWindowAsync(window, cancellationToken).ConfigureAwait(false);
                _hydraWindow = null;
            }
            if (_owned is null) return;
            await _backend.CloseAsync(_owned, cancellationToken).ConfigureAwait(false);
            _owned = null;
        }
        finally { _gate.Release(); }
    }
}

internal sealed record LauncherSpec(string Executable, string Argument, LauncherKind Kind);
internal sealed record OwnedLauncher(int ProcessId, DateTime StartTimeUtc, string Executable, LauncherKind Kind);
internal sealed record HydraWindow(nint Handle, OwnedLauncher Owner);

internal interface IWindowsLauncherBackend
{
    LauncherSpec Resolve(LauncherKind kind);
    OwnedLauncher? FindExistingSteam(string executable);
    OwnedLauncher? Start(LauncherSpec spec, bool trackOwnership);
    bool IsAlive(OwnedLauncher process);
    IReadOnlyList<HydraWindow> HydraWindows(string executable);
    Task<HydraWindow?> FindNewHydraWindowAsync(LauncherSpec spec, IReadOnlyList<HydraWindow> before, CancellationToken cancellationToken);
    Task CloseHydraWindowAsync(HydraWindow window, CancellationToken cancellationToken);
    Task CloseAsync(OwnedLauncher process, CancellationToken cancellationToken);
}

internal sealed class WindowsLauncherBackend : IWindowsLauncherBackend
{
    public LauncherSpec Resolve(LauncherKind kind)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        string? executable;
        var argument = ArgumentFor(kind);
        switch (kind)
        {
            case LauncherKind.Steam:
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    executable = key?.GetValue("SteamExe") as string;
                break;
            case LauncherKind.Hydra:
                executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Hydra", "Hydra.exe");
                break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            throw new FileNotFoundException("Selected launcher is unavailable.");
        return new LauncherSpec(Path.GetFullPath(executable), argument, kind);
    }

    internal static string ArgumentFor(LauncherKind kind) => kind switch
    {
        LauncherKind.Steam => "-gamepadui",
        LauncherKind.Hydra => "--big-picture",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool IsRunning(string executable)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable));
        try { return processes.Length != 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    public OwnedLauncher? FindExistingSteam(string executable)
    {
        using var self = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable));
        try
        {
            OwnedLauncher? existing = null;
            foreach (var process in processes)
            {
                if (process.SessionId != self.SessionId || process.HasExited ||
                    !string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) continue;
                if (existing is not null) throw new InvalidOperationException("Multiple Steam clients match the launcher path.");
                existing = new(process.Id, process.StartTime.ToUniversalTime(), executable, LauncherKind.Steam);
            }
            return existing;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    public OwnedLauncher? Start(LauncherSpec spec, bool trackOwnership)
    {
        var info = new ProcessStartInfo(spec.Executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(spec.Executable)! };
        info.ArgumentList.Add(spec.Argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Launcher did not start.");
        return trackOwnership ? new OwnedLauncher(process.Id, process.StartTime.ToUniversalTime(), spec.Executable, spec.Kind) : null;
    }

    public bool IsAlive(OwnedLauncher process)
    {
        try { using var candidate = Process.GetProcessById(process.ProcessId); return Matches(candidate, process) && !candidate.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public IReadOnlyList<HydraWindow> HydraWindows(string executable)
    {
        var windows = new List<HydraWindow>();
        using var self = Process.GetCurrentProcess();
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                if (process.SessionId == self.SessionId &&
                    process.ProcessName.Equals(Path.GetFileNameWithoutExtension(executable), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase))
                    windows.Add(new(handle, new(process.Id, process.StartTime.ToUniversalTime(), executable, LauncherKind.Hydra)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return true;
        }, 0);
        return windows;
    }

    public async Task<HydraWindow?> FindNewHydraWindowAsync(LauncherSpec spec, IReadOnlyList<HydraWindow> before, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullscreen = HydraWindows(spec.Executable).Where(window => IsFullscreen(window.Handle)).ToArray();
            if (fullscreen.Length == 1)
            {
                var window = fullscreen[0];
                if (!before.Contains(window))
                {
                    SetForegroundWindow(window.Handle);
                    return window;
                }
                SetForegroundWindow(window.Handle);
                return null;
            }
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Could not identify the Hydra Big Picture window.");
    }

    public async Task CloseHydraWindowAsync(HydraWindow window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HydraWindows(window.Owner.Executable).Contains(window)) return;
        if (!PostMessage(window.Handle, 0x0010, 0, 0))
            throw new InvalidOperationException("Could not request Hydra Big Picture window closure.");
        while (HydraWindows(window.Owner.Executable).Contains(window))
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsFullscreen(nint handle)
    {
        const int windowStyle = -16;
        const int caption = 0x00C00000;
        if (!IsWindowVisible(handle) || (GetWindowLong(handle, windowStyle) & caption) != 0 ||
            !GetWindowRect(handle, out var bounds)) return false;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor) && bounds.Equals(monitor.Bounds);
    }

    private delegate bool WindowCallback(nint handle, nint parameter);
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect Bounds, WorkArea; public int Flags; }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint pid);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint handle, int index);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint handle, out Rect bounds);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint handle, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool PostMessage(nint handle, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public async Task CloseAsync(OwnedLauncher process, CancellationToken cancellationToken)
    {
        Process candidate;
        try { candidate = Process.GetProcessById(process.ProcessId); }
        catch (ArgumentException) { VerifyNoReplacement(process); return; }
        using (candidate)
        {
            if (!Matches(candidate, process) || candidate.HasExited) { VerifyNoReplacement(process); return; }
            cancellationToken.ThrowIfCancellationRequested();
            if (process.Kind == LauncherKind.Steam)
            {
                var command = new ProcessStartInfo(process.Executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(process.Executable)! };
                command.ArgumentList.Add(SteamExitBigPictureArgument);
                using var request = Process.Start(command) ?? throw new InvalidOperationException("Could not request exit from Big Picture.");
                await request.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (request.ExitCode != 0) throw new InvalidOperationException("Steam rejected the Big Picture exit request.");
                return;
            }
            if (!candidate.CloseMainWindow()) throw new InvalidOperationException("The owned launcher has no closable window.");
            await candidate.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal const string SteamExitBigPictureArgument = "steam://close/bigpicture";
    internal const string SteamOpenBigPictureArgument = "steam://open/bigpicture";

    private void VerifyNoReplacement(OwnedLauncher process)
    {
        EnsureNoReplacement(IsRunning(process.Executable));
    }

    internal static void EnsureNoReplacement(bool launcherStillRunning)
    {
        if (launcherStillRunning)
            throw new InvalidOperationException("The launcher process changed; its remaining instance cannot be safely closed.");
    }

    private static bool Matches(Process candidate, OwnedLauncher expected) =>
        candidate.StartTime.ToUniversalTime() == expected.StartTimeUtc &&
        string.Equals(candidate.MainModule?.FileName, expected.Executable, StringComparison.OrdinalIgnoreCase);
}

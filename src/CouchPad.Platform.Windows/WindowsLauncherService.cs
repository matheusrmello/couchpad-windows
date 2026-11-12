using System.Diagnostics;
using System.Runtime.InteropServices;
using CouchPad.Core.Launchers;
using Microsoft.Win32;

namespace CouchPad.Platform.Windows;

/// <summary>Launches a selected launcher and only requests closure of its own process.</summary>
public sealed class WindowsLauncherService : ILauncherService
{
    private readonly IWindowsLauncherBackend _backend;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private OwnedLauncher? _owned;

    public WindowsLauncherService() : this(new WindowsLauncherBackend()) { }
    internal WindowsLauncherService(IWindowsLauncherBackend backend) => _backend = backend;

    public async Task StartAsync(LauncherSelection selection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_owned is not null && _backend.IsAlive(_owned)) return;
            _owned = null;
            await Task.Run(async () =>
            {
                var spec = _backend.Resolve(selection.Kind);
                cancellationToken.ThrowIfCancellationRequested();
                var preexisting = _backend.IsRunning(spec.Executable);
                cancellationToken.ThrowIfCancellationRequested();
                _owned = _backend.Start(spec, !preexisting);
                if (preexisting && spec.Kind == LauncherKind.Hydra)
                    await _backend.ActivateExistingAsync(spec, cancellationToken).ConfigureAwait(false);
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

    public async Task CloseOwnedAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_owned is null) return;
            await _backend.CloseAsync(_owned, cancellationToken).ConfigureAwait(false);
            _owned = null;
        }
        finally { _gate.Release(); }
    }
}

internal sealed record LauncherSpec(string Executable, string Argument, LauncherKind Kind);
internal sealed record OwnedLauncher(int ProcessId, DateTime StartTimeUtc, string Executable, LauncherKind Kind);

internal interface IWindowsLauncherBackend
{
    LauncherSpec Resolve(LauncherKind kind);
    bool IsRunning(string executable);
    OwnedLauncher? Start(LauncherSpec spec, bool trackOwnership);
    bool IsAlive(OwnedLauncher process);
    Task ActivateExistingAsync(LauncherSpec spec, CancellationToken cancellationToken);
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

    public bool IsRunning(string executable)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable));
        try { return processes.Length != 0; }
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

    public async Task ActivateExistingAsync(LauncherSpec spec, CancellationToken cancellationToken)
    {
        // The Hydra single-instance handler opens its own window asynchronously.
        // Do not close or claim ownership of any existing instance.
        await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(spec.Executable));
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        if (!string.Equals(process.MainModule?.FileName, spec.Executable, StringComparison.OrdinalIgnoreCase)) continue;
                        var window = process.MainWindowHandle;
                        if (window == IntPtr.Zero) continue;
                        ShowWindowAsync(window, 9); // SW_RESTORE
                        if (SetForegroundWindow(window)) return;
                    }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                }
            }
            finally { foreach (var process in processes) process.Dispose(); }
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Could not bring the existing Hydra window to the foreground.");
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int command);

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

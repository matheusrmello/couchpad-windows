using System.Diagnostics;
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

internal sealed record LauncherSpec(string Executable, string Argument);
internal sealed record OwnedLauncher(int ProcessId, DateTime StartTimeUtc, string Executable);

internal interface IWindowsLauncherBackend
{
    LauncherSpec Resolve(LauncherKind kind);
    bool IsRunning(string executable);
    OwnedLauncher? Start(LauncherSpec spec, bool trackOwnership);
    bool IsAlive(OwnedLauncher process);
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
        return new LauncherSpec(Path.GetFullPath(executable), argument);
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
        return trackOwnership ? new OwnedLauncher(process.Id, process.StartTime.ToUniversalTime(), spec.Executable) : null;
    }

    public bool IsAlive(OwnedLauncher process)
    {
        try { using var candidate = Process.GetProcessById(process.ProcessId); return Matches(candidate, process) && !candidate.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public async Task CloseAsync(OwnedLauncher process, CancellationToken cancellationToken)
    {
        Process candidate;
        try { candidate = Process.GetProcessById(process.ProcessId); }
        catch (ArgumentException) { VerifyNoReplacement(process); return; }
        using (candidate)
        {
            if (!Matches(candidate, process) || candidate.HasExited) { VerifyNoReplacement(process); return; }
            cancellationToken.ThrowIfCancellationRequested();
            if (!candidate.CloseMainWindow()) throw new InvalidOperationException("The owned launcher has no closable window.");
            await candidate.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

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

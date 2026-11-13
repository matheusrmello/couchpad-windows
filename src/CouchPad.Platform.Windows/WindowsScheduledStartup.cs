using System.Security.Principal;

namespace CouchPad.Platform.Windows;

public sealed class WindowsScheduledStartup
{
    private readonly RescueLogonTaskRegistration _task;
    private readonly WindowsStartupRegistration _legacy;
    private readonly string _sid;

    public WindowsScheduledStartup() : this(new WindowsRescueLogonTaskStore(), new WindowsStartupRegistration(), CurrentSid()) { }

    internal WindowsScheduledStartup(IRescueLogonTaskStore tasks, WindowsStartupRegistration legacy, string sid)
    {
        _task = new RescueLogonTaskRegistration(tasks, launchApp: true);
        _legacy = legacy;
        _sid = sid;
    }

    public bool ReadAndMigrate(string appPath)
    {
        var rescue = RescuePath(appPath);
        var scheduled = _task.IsRegistered(rescue, _sid);
        if (!_legacy.IsEnabled(appPath)) return scheduled;
        if (!scheduled) _task.Create(rescue, _sid);
        _legacy.SetEnabled(false, appPath);
        return true;
    }

    public void SetEnabled(bool enabled, string appPath)
    {
        var rescue = RescuePath(appPath);
        var hasLegacy = _legacy.IsEnabled(appPath);
        if (enabled) _task.Create(rescue, _sid);
        else
        {
            // Check ownership before modifying either startup mechanism.
            _task.IsRegistered(rescue, _sid);
            _task.Remove(rescue, _sid);
        }
        if (hasLegacy) _legacy.SetEnabled(false, appPath);
    }

    private static string RescuePath(string appPath) =>
        Path.Combine(Path.GetDirectoryName(appPath)!, "CouchPad.Rescue.exe");

    internal void RemoveForUninstall(string appPath)
    {
        _task.Remove(RescuePath(appPath), _sid, preserveOtherInstallations: true);
        _legacy.RemoveIfOwned(appPath);
    }

    internal void CheckOwnership(string appPath)
    {
        _ = _task.IsRegistered(RescuePath(appPath), _sid);
        _ = _legacy.IsEnabled(appPath);
    }

    private static string CurrentSid()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? throw new InvalidOperationException("Windows user SID unavailable.");
    }
}

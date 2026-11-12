using System.Diagnostics;

namespace CouchPad.Platform.Windows;

public static class WindowsAssistedRecovery
{
    public static void Start(string rescuePath)
    {
        var info = new ProcessStartInfo(rescuePath) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("--assisted-recovery");
        using var started = Process.Start(info) ?? throw new InvalidOperationException("Rescue could not be started.");
    }
}

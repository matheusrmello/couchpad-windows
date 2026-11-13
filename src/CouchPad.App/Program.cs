using Avalonia;
using System.Diagnostics;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

namespace CouchPad.App;

internal static class Program
{
    internal static WindowsAppInstance? Instance { get; private set; }
    internal static bool OwnsDisplaySession { get; private set; }
    internal static bool CanRestartForHardware =>
        string.Equals(Environment.ProcessPath, Path.Combine(AppContext.BaseDirectory, "CouchPad.exe"),
            StringComparison.OrdinalIgnoreCase);

    internal static void StartHardwareRestart()
    {
        if (!CanRestartForHardware) throw new InvalidOperationException("The installed CouchPad executable is required.");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        start.ArgumentList.Add("--apply-hardware-after");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var child = Process.Start(start) ?? throw new InvalidOperationException("CouchPad could not restart.");
    }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["--apply-hardware-after", var parent])
        {
            if (!int.TryParse(parent, out var parentId) || parentId <= 0 || parentId == Environment.ProcessId ||
                !WaitForHardwareRestart(parentId)) return 1;
            args = ["--hardware-applied"];
        }
        if (!OperatingSystem.IsWindows() || args.Contains("--smoke") || args.Contains("--gallery") || args.Contains("--simulate"))
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        try { Instance = WindowsAppInstance.AcquireOrForwardAsync(AppPaths.GetRoot()).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return 1;
        }
        if (Instance is null) return 0;
        using (Instance)
        {
            DisplaySessionOwner owner;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            try
            {
                (owner, _) = DisplaySessionOwner.AcquireWhenAvailableAsync(AppPaths.SessionJournalFile(), deadline.Token)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                return 1;
            }
            using (owner)
            {
                OwnsDisplaySession = true;
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }
    }

    private static bool WaitForHardwareRestart(int parentId)
    {
        try
        {
            using var process = Process.GetProcessById(parentId);
            return process.WaitForExit(15_000);
        }
        catch (ArgumentException) { return true; }
        catch (InvalidOperationException) { return true; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

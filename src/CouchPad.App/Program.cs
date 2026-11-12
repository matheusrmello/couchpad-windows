using Avalonia;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

namespace CouchPad.App;

internal static class Program
{
    internal static WindowsAppInstance? Instance { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Contains("--smoke") || args.Contains("--gallery"))
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        try { Instance = WindowsAppInstance.AcquireOrForwardAsync(AppPaths.GetRoot()).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return 1;
        }
        if (Instance is null) return 0;
        using (Instance)
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

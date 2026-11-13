using Avalonia;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

namespace CouchPad.App;

internal static class Program
{
    internal static WindowsAppInstance? Instance { get; private set; }
    internal static bool OwnsDisplaySession { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
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

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

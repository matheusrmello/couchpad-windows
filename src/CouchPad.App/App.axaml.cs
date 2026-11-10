using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.App.Views;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Localization;
using CouchPad.Core.Settings;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

namespace CouchPad.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var isSmoke = desktop.Args?.Contains("--smoke", StringComparer.Ordinal) == true;
            var isGallery = desktop.Args?.Contains("--gallery", StringComparer.Ordinal) == true;
            var settingsPath = isSmoke
                ? Path.Combine(Path.GetTempPath(), "couchpad-smoke", "settings.json")
                : AppPaths.SettingsFile();
            var journalPath = isSmoke
                ? Path.Combine(Path.GetTempPath(), "couchpad-smoke", "state", "session.json")
                : AppPaths.SessionJournalFile();

            var settingsStore = new JsonAppSettingsStore(settingsPath);
            LocalizationService.Instance.Initialize(settingsStore);

            var (displayService, rescueGuard) = CreateWindowsDisplayServices(
                desktop.Args ?? [], journalPath, isSmoke || isGallery,
                Path.Combine(AppContext.BaseDirectory, "CouchPad.Rescue.exe"));

            Window window = isGallery
                ? new ComponentGalleryWindow()
                : new MainWindow
                {
                    DataContext = new MainWindowViewModel(
                        LocalizationService.Instance,
                        settingsStore,
                        new JsonSessionJournalStore(journalPath),
                        displayService: displayService,
                        rescueGuard: rescueGuard),
                };

            desktop.MainWindow = window;

            if (isSmoke)
            {
                window.Opened += async (_, _) =>
                {
                    await Task.Delay(600);

                    var localization = LocalizationService.Instance;
                    var initial = localization.Language;

                    localization.SetLanguage(AppLanguage.PtBr);
                    var pt = localization["automation.toggle"];
                    localization.SetLanguage(AppLanguage.En);
                    var en = localization["automation.toggle"];

                    Console.WriteLine(
                        $"SMOKE: window opened {window.Bounds.Width}x{window.Bounds.Height}; " +
                        $"initial={initial}; pt='{pt}'; en='{en}'; " +
                        $"language={localization.Language}; title='{window.Title}'");

                    window.Close();
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static (IDisplayService? Display, IRescueGuard? Guard) CreateWindowsDisplayServices(
        string[] args, string journalPath, bool simulationOnly, string rescuePath)
    {
        const string flag = "--windows-display";
        var positions = args.Select((value, index) => (value, index))
            .Where(item => string.Equals(item.value, flag, StringComparison.Ordinal)).ToArray();
        if (positions.Length == 0 || simulationOnly) return (null, null);
        if (!OperatingSystem.IsWindows() || positions.Length != 1 || positions[0].index + 6 >= args.Length)
            throw new ArgumentException("Invalid Windows display configuration.");
        var i = positions[0].index;
        if (string.IsNullOrWhiteSpace(args[i + 1]) || string.IsNullOrWhiteSpace(args[i + 4]) ||
            !ushort.TryParse(args[i + 2], out var tvManufacturer) || !ushort.TryParse(args[i + 3], out var tvProduct) ||
            !ushort.TryParse(args[i + 5], out var monitorManufacturer) || !ushort.TryParse(args[i + 6], out var monitorProduct))
            throw new ArgumentException("Invalid Windows display selector.");
        if (!File.Exists(rescuePath)) throw new FileNotFoundException("Rescue executable is unavailable.", rescuePath);
        var options = new WindowsDisplayOptions(args[i + 1], tvManufacturer, tvProduct,
            AllowWrites: false, InternalDisplay: new DisplaySelector(args[i + 4], monitorManufacturer, monitorProduct));
        var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(journalPath))!;
        var transaction = new RescueTransactionContext(stateDirectory);
        return (new WindowsDisplayService(options, transaction),
            new WindowsRescueGuard(rescuePath, journalPath, TimeSpan.FromSeconds(60), transaction: transaction));
    }
}

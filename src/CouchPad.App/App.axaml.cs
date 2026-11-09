using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.App.Views;
using CouchPad.Core.Localization;
using CouchPad.Core.Settings;
using CouchPad.Core.Storage;

namespace CouchPad.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var isSmoke = desktop.Args?.Contains("--smoke", StringComparer.Ordinal) == true;
            var settingsPath = isSmoke
                ? Path.Combine(Path.GetTempPath(), "couchpad-smoke", "settings.json")
                : AppPaths.SettingsFile();

            LocalizationService.Instance.Initialize(new JsonAppSettingsStore(settingsPath));

            var window = new MainWindow
            {
                DataContext = new MainWindowViewModel(LocalizationService.Instance),
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
}

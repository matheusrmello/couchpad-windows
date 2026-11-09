using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.App.Views;
using CouchPad.Core.Settings;

namespace CouchPad.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            LocalizationService.Instance.Initialize(new JsonAppSettingsStore());

            var window = new MainWindow
            {
                DataContext = new MainWindowViewModel(LocalizationService.Instance),
            };

            desktop.MainWindow = window;

            if (desktop.Args?.Contains("--smoke", StringComparer.Ordinal) == true)
            {
                window.Opened += async (_, _) =>
                {
                    await Task.Delay(600);
                    Console.WriteLine(
                        $"SMOKE: window opened {window.Bounds.Width}x{window.Bounds.Height}; " +
                        $"language={LocalizationService.Instance.Language}; " +
                        $"title='{window.Title}'");
                    window.Close();
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}

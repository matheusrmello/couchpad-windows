using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CouchPad.App.Localization;
using CouchPad.Core.Localization;

namespace CouchPad.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly LocalizationService _localization;

    public MainWindowViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    public bool IsPortuguese => _localization.Language == AppLanguage.PtBr;

    public bool IsEnglish => _localization.Language == AppLanguage.En;

    public string VersionText
    {
        get
        {
            var informational = typeof(MainWindowViewModel).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var version = informational?.Split('+')[0] ?? "0.0.0";

            return string.Format(_localization["footer.version"], version);
        }
    }

    [RelayCommand]
    private void SetPortuguese() => _localization.SetLanguage(AppLanguage.PtBr);

    [RelayCommand]
    private void SetEnglish() => _localization.SetLanguage(AppLanguage.En);

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(IsPortuguese));
        OnPropertyChanged(nameof(IsEnglish));
    }
}

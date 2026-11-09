using System.ComponentModel;
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

    public string VersionText => string.Format(
        _localization["footer.version"],
        typeof(MainWindowViewModel).Assembly.GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "0.1.0");

    [RelayCommand]
    private void SetPortuguese() => _localization.SetLanguage(AppLanguage.PtBr);

    [RelayCommand]
    private void SetEnglish() => _localization.SetLanguage(AppLanguage.En);

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(VersionText));
}

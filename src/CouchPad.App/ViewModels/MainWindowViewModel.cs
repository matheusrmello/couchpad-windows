using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CouchPad.App.Localization;
using CouchPad.Core.Localization;

namespace CouchPad.App.ViewModels;

/// <summary>
/// Estado do shell da P1: apenas UI e valores estáticos.
/// Nenhuma chamada de hardware acontece aqui.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly LocalizationService _localization;

    [ObservableProperty]
    private bool _automationEnabled;

    [ObservableProperty]
    private bool _closeLauncherOnActivate = true;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private int _activationDelayIndex;

    [ObservableProperty]
    private int _deactivationDelayIndex;

    [ObservableProperty]
    private int _displayModeIndex;

    [ObservableProperty]
    private int _audioModeIndex;

    [ObservableProperty]
    private bool _steamEnabled = true;

    [ObservableProperty]
    private string? _customLauncherPath;

    [ObservableProperty]
    private bool _isRefreshing;

    public MainWindowViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    public bool IsPortuguese => _localization.Language == AppLanguage.PtBr;

    public bool IsEnglish => _localization.Language == AppLanguage.En;

    /// <summary>Índice do seletor de idioma: 0 = pt-BR, 1 = inglês.</summary>
    public int LanguageIndex
    {
        get => IsPortuguese ? 0 : 1;
        set
        {
            if (value < 0 || value == LanguageIndex)
            {
                return;
            }

            _localization.SetLanguage(value == 0 ? AppLanguage.PtBr : AppLanguage.En);
            OnPropertyChanged();
        }
    }

    /// <summary>Sem hardware conectado, a automação ligada fica aguardando o gamepad.</summary>
    public string StatusText =>
        _localization[AutomationEnabled ? "status.waiting" : "status.idle"];

    public bool StatusIsWaiting => AutomationEnabled;

    public string DisplayRefreshText =>
        _localization[IsRefreshing ? "display.refreshing" : "display.refresh"];

    public string CustomLauncherPathText =>
        string.IsNullOrWhiteSpace(CustomLauncherPath)
            ? _localization["launchers.no_path"]
            : CustomLauncherPath;

    public string LauncherSummary =>
        !string.IsNullOrWhiteSpace(CustomLauncherPath)
            ? _localization["launchers.custom"]
            : SteamEnabled
                ? _localization["launchers.steam"]
                : _localization["automation.no_launcher"];

    public string ChooseFolderDialogTitle => _localization["launchers.choose_title"];

    public string VersionText
    {
        get
        {
            var informational = typeof(MainWindowViewModel).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var version = informational?.Split('+')[0] ?? "0.0.0";
            if (string.IsNullOrWhiteSpace(version))
            {
                version = "0.0.0";
            }

            return string.Format(_localization["footer.version"], version);
        }
    }

    [RelayCommand]
    private async Task RefreshDisplaysAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            // Placeholder de carregamento; a leitura real de displays entra na P4.
            await Task.Delay(700);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        AutomationEnabled = false;
        CloseLauncherOnActivate = true;
        StartWithWindows = false;
        MinimizeToTray = true;
        ActivationDelayIndex = 0;
        DeactivationDelayIndex = 0;
        DisplayModeIndex = 0;
        AudioModeIndex = 0;
        SteamEnabled = true;
        CustomLauncherPath = null;
        IsRefreshing = false;
    }

    partial void OnAutomationEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIsWaiting));
    }

    partial void OnIsRefreshingChanged(bool value) =>
        OnPropertyChanged(nameof(DisplayRefreshText));

    partial void OnSteamEnabledChanged(bool value) =>
        OnPropertyChanged(nameof(LauncherSummary));

    partial void OnCustomLauncherPathChanged(string? value)
    {
        OnPropertyChanged(nameof(CustomLauncherPathText));
        OnPropertyChanged(nameof(LauncherSummary));
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsPortuguese));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(LanguageIndex));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DisplayRefreshText));
        OnPropertyChanged(nameof(CustomLauncherPathText));
        OnPropertyChanged(nameof(LauncherSummary));
        OnPropertyChanged(nameof(VersionText));
    }
}

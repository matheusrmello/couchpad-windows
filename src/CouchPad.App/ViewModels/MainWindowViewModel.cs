using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CouchPad.App.Controls;
using CouchPad.App.Localization;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;
using CouchPad.Core.Localization;
using CouchPad.Core.Launchers;
using CouchPad.Core.Settings;

namespace CouchPad.App.ViewModels;

/// <summary>
/// Shell + simulação da P3. Nenhuma chamada de hardware: display, áudio e
/// gamepad são serviços simulados no Core.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly IAppSettingsStore _settings;
    private readonly IGamepadWatcher _gamepad;
    private readonly ISimulatedGamepad? _gamepadSimulator;
    private readonly AutomationCoordinator _coordinator;
    private readonly IDisplayWriteControl? _displayWriteControl;
    private readonly Func<IReadOnlyList<DisplayTargetOption>>? _discoverTargets;
    private readonly Func<IReadOnlyList<AudioEndpointOption>>? _discoverAudioEndpoints;
    private readonly Action? _revokeAudioWrites;
    private readonly CountdownNotificationPresenter _countdownNotifications;
    private bool _isLoadingSettings;
    private bool _audioWritesRevoked;
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private long _countdownDeadline;

    [ObservableProperty]
    private bool _automationEnabled;

    [ObservableProperty]
    private bool _closeLauncherOnActivate = true;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _useWindowsGamepad;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private int _activationDelayIndex = 2;

    [ObservableProperty]
    private int _deactivationDelayIndex;

    [ObservableProperty]
    private int _displayModeIndex;

    [ObservableProperty]
    private int _audioModeIndex;

    [ObservableProperty]
    private bool _steamEnabled = true;

    [ObservableProperty]
    private int _launcherIndex;

    [ObservableProperty]
    private string? _customLauncherPath;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _allowDisplayWrites;

    [ObservableProperty]
    private bool _allowAudioWrites;

    [ObservableProperty]
    private AudioEndpointOption? _selectedTvAudioEndpoint;

    [ObservableProperty]
    private DisplayTargetOption? _selectedTvTarget;

    [ObservableProperty]
    private DisplayTargetOption? _selectedInternalTarget;

    [ObservableProperty]
    private string? _displayTargetsError;

    public ObservableCollection<DisplayTargetOption> DisplayTargets { get; } = new();
    public ObservableCollection<AudioEndpointOption> AudioEndpoints { get; } = new();

    public MainWindowViewModel(
        LocalizationService localization,
        IAppSettingsStore settingsStore,
        ISessionJournalStore? journalStore = null,
        IGamepadWatcher? gamepadWatcher = null,
        IDisplayService? displayService = null,
        IAudioService? audioService = null,
        IDelayScheduler? delayScheduler = null,
        IRescueGuard? rescueGuard = null,
        Func<IReadOnlyList<DisplayTargetOption>>? discoverTargets = null,
        Func<IReadOnlyList<AudioEndpointOption>>? discoverAudioEndpoints = null,
        Action? revokeAudioWrites = null,
        Action<TimeSpan, string, string>? showCountdown = null,
        Action? dismissCountdown = null,
        ILauncherService? launcherService = null)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _countdownTimer.Tick += (_, _) => OnPropertyChanged(nameof(StatusText));
        _settings = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _discoverTargets = discoverTargets;
        _discoverAudioEndpoints = discoverAudioEndpoints;
        _revokeAudioWrites = revokeAudioWrites;
        _countdownNotifications = new CountdownNotificationPresenter(showCountdown, dismissCountdown);
        _displayWriteControl = displayService as IDisplayWriteControl;
        _localization.PropertyChanged += OnLocalizationChanged;

        _gamepad = gamepadWatcher ?? new SimulatedGamepadWatcher();
        _gamepadSimulator = _gamepad as ISimulatedGamepad;
        _coordinator = new AutomationCoordinator(
            displayService ?? new SimulatedDisplayService(),
            audioService ?? new SimulatedAudioService(),
            delayScheduler ?? new SystemDelayScheduler(),
            journalStore ?? new JsonSessionJournalStore(),
            rescueGuard: rescueGuard,
            launcher: launcherService);

        _coordinator.StateChanged += OnAutomationStateChanged;
        _gamepad.ConnectionChanged += OnGamepadConnectionChanged;

        LoadSettings();
        RefreshDisplayTargets();
        RefreshAudioEndpoints();
        _ = InitializeAutomationAsync();
    }

    /// <summary>A view abre o seletor de pastas e devolve o caminho escolhido.</summary>
    public event EventHandler? FolderPickerRequested;

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

    public string StatusText => _coordinator.State == AutomationState.Pending && _countdownDeadline != 0
        ? string.Format(_localization["status.countdown"], Math.Max(0, (int)Math.Ceiling((double)(_countdownDeadline - Stopwatch.GetTimestamp()) / Stopwatch.Frequency)))
        : _localization[StatusKey];

    public bool IsRecoveryUncertain => _coordinator.IsRecoveryUncertain;

    public bool HasLauncherError => _coordinator.LauncherFailed;

    public string LauncherErrorText => _localization["launchers.failed"];

    public string RecoveryHelpText => _localization["recovery.help"];

    public StatusTone CurrentStatusTone => _coordinator.State switch
    {
        AutomationState.WaitingForGamepad or AutomationState.Pending => StatusTone.Warning,
        AutomationState.Applying or AutomationState.Active => StatusTone.Accent,
        AutomationState.Error or AutomationState.RescuePending => StatusTone.Danger,
        _ => StatusTone.Neutral,
    };

    public bool IsGamepadConnected => _gamepad.Current is not null;

    public bool IsSimulationAvailable => _gamepadSimulator is not null;

    public string GamepadSummary
    {
        get
        {
            if (_gamepad.Current is not { } device)
            {
                return _localization["gamepad.none"];
            }

            return IsSimulationAvailable ? _localization["gamepad.simulated"] : device.Name;
        }
    }

    public string GamepadActionText => _localization[
        IsGamepadConnected ? "gamepad.disconnect" : "gamepad.connect"];

    public StatusTone GamepadTone =>
        IsGamepadConnected ? StatusTone.Accent : StatusTone.Neutral;

    public string DisplaySummary
    {
        get
        {
            if (_coordinator.State == AutomationState.Active &&
                _coordinator.Snapshot is { } snapshot)
            {
                return snapshot.DisplayMode switch
                {
                    DisplayMode.Extend => _localization["display.active.extend"],
                    DisplayMode.Mirror => _localization["display.active.mirror"],
                    _ => _localization["display.active.tv_only"],
                };
            }

            return _localization["display.current_value"];
        }
    }

    public string AudioSummary =>
        _coordinator.State == AutomationState.Active &&
        _coordinator.Snapshot is { AppliedAudio: AudioOutput.Hdmi }
            ? _localization["audio.active.hdmi"]
            : _localization["automation.default_audio"];

    public string DisplayRefreshText =>
        _localization[IsRefreshing ? "display.refreshing" : "display.refresh"];

    public string DisplayTargetHint => _localization["display.target_hint"];

    public bool HasDisplayTargetsError => !string.IsNullOrEmpty(DisplayTargetsError);

    public ActionState DisplayRefreshState =>
        IsRefreshing ? ActionState.Loading : ActionState.Idle;

    public string CustomLauncherPathText =>
        string.IsNullOrWhiteSpace(CustomLauncherPath)
            ? _localization["launchers.no_path"]
            : CustomLauncherPath;

    public string LauncherSummary => SteamEnabled
        ? _localization[LauncherIndex == 1 ? "launchers.hydra" : "launchers.steam"]
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

    private string StatusKey => _coordinator.State switch
    {
        AutomationState.WaitingForGamepad => "status.waiting",
        AutomationState.Pending => "status.pending",
        AutomationState.Applying => "status.applying",
        AutomationState.Active => "status.active",
        AutomationState.Error => "status.error",
        AutomationState.RescuePending => "status.rescue_pending",
        _ => "status.idle",
    };

    [RelayCommand]
    private void RefreshDisplayTargets()
    {
        if (_discoverTargets is null)
        {
            return;
        }

        var tv = SelectedTvTarget;
        var internalDisplay = SelectedInternalTarget;
        _isLoadingSettings = true;

        try
        {
            DisplayTargets.Clear();
            foreach (var target in _discoverTargets())
            {
                AddTarget(target);
            }

            tv = EnsureTarget(tv);
            internalDisplay = EnsureTarget(internalDisplay);
            SelectedTvTarget = tv;
            SelectedInternalTarget = internalDisplay;
            DisplayTargetsError = null;
        }
        catch
        {
            DisplayTargetsError = _localization["display.targets_error"];
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    [RelayCommand]
    private void RefreshAudioEndpoints()
    {
        if (_discoverAudioEndpoints is null) return;
        var selected = SelectedTvAudioEndpoint;
        _isLoadingSettings = true;
        try
        {
            AudioEndpoints.Clear();
            foreach (var endpoint in _discoverAudioEndpoints().DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
                AudioEndpoints.Add(endpoint);
            if (selected is not null)
            {
                var match = AudioEndpoints.FirstOrDefault(e => string.Equals(e.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
                if (match is null) { AudioEndpoints.Add(selected); match = selected; }
                SelectedTvAudioEndpoint = match;
            }
        }
        finally { _isLoadingSettings = false; }
    }

    private DisplayTargetOption? EnsureTarget(DisplayTargetOption? target)
    {
        if (target is null)
        {
            return null;
        }

        var existing = DisplayTargets.FirstOrDefault(item => item.Matches(target));
        if (existing is not null)
        {
            return existing;
        }

        AddTarget(target);
        return target;
    }

    private void AddTarget(DisplayTargetOption target)
    {
        if (!DisplayTargets.Any(item => item.Matches(target)))
        {
            DisplayTargets.Add(target);
        }
    }

    private static DisplayTargetOption? CreateSavedTarget(
        string? devicePath,
        ushort? manufacturer,
        ushort? product,
        string? exactName) =>
        string.IsNullOrWhiteSpace(devicePath)
            ? null
            : new DisplayTargetOption(devicePath, manufacturer, product, exactName);

    [RelayCommand]
    private void ToggleGamepad()
    {
        if (_gamepadSimulator is null)
        {
            return;
        }

        if (_gamepad.Current is not null)
        {
            _gamepadSimulator.Disconnect();
        }
        else
        {
            _gamepadSimulator.Connect();
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
            // Leitura real de displays entra na P4; aqui só exercita o estado.
            await Task.Delay(700);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void ChooseLauncherFolder() => FolderPickerRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Reset()
    {
        _displayWriteControl?.DisableNewWrites();
        _revokeAudioWrites?.Invoke();
        if (_revokeAudioWrites is not null) _audioWritesRevoked = true;
        _isLoadingSettings = true;
        try
        {
            AutomationEnabled = false;
            CloseLauncherOnActivate = true;
            StartWithWindows = false;
            UseWindowsGamepad = false;
            MinimizeToTray = true;
            ActivationDelayIndex = 2;
            DeactivationDelayIndex = 0;
            DisplayModeIndex = 0;
            AudioModeIndex = 0;
            SteamEnabled = true;
            LauncherIndex = 0;
            CustomLauncherPath = null;
            AllowDisplayWrites = false;
            AllowAudioWrites = false;
            SelectedTvAudioEndpoint = null;
            SelectedTvTarget = null;
            SelectedInternalTarget = null;
            DisplayTargetsError = null;
            IsRefreshing = false;
        }
        finally
        {
            _isLoadingSettings = false;
        }

        PersistSettings();
        SyncAutomation();
    }

    partial void OnAutomationEnabledChanged(bool value)
    {
        if (value && (_displayWriteControl is { CanApply: false } || (AllowAudioWrites && _audioWritesRevoked)))
        {
            AutomationEnabled = false;
            return;
        }
        PersistSettings();
        SyncAutomation();
    }

    partial void OnCloseLauncherOnActivateChanged(bool value)
    {
        PersistSettings();
        SyncAutomation();
    }

    partial void OnStartWithWindowsChanged(bool value) => PersistSettings();

    partial void OnUseWindowsGamepadChanged(bool value) => PersistSettings();

    partial void OnMinimizeToTrayChanged(bool value) => PersistSettings();

    partial void OnActivationDelayIndexChanged(int value)
    {
        PersistSettings();
        SyncAutomation();
    }

    partial void OnDeactivationDelayIndexChanged(int value)
    {
        PersistSettings();
        SyncAutomation();
    }

    partial void OnDisplayModeIndexChanged(int value)
    {
        PersistSettings();
        SyncAutomation();
        OnPropertyChanged(nameof(DisplaySummary));
    }

    partial void OnAudioModeIndexChanged(int value)
    {
        PersistSettings();
        SyncAutomation();
        OnPropertyChanged(nameof(AudioSummary));
    }

    partial void OnSteamEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(LauncherSummary));
        PersistSettings();
        SyncAutomation();
    }

    partial void OnLauncherIndexChanged(int value)
    {
        OnPropertyChanged(nameof(LauncherSummary));
        PersistSettings();
        SyncAutomation();
    }

    partial void OnCustomLauncherPathChanged(string? value)
    {
        OnPropertyChanged(nameof(CustomLauncherPathText));
        OnPropertyChanged(nameof(LauncherSummary));
        PersistSettings();
    }

    partial void OnAllowDisplayWritesChanged(bool value)
    {
        if (!_isLoadingSettings && !value)
        {
            _displayWriteControl?.DisableNewWrites();
            if (AutomationEnabled) AutomationEnabled = false;
        }
        PersistSettings();
    }

    partial void OnAllowAudioWritesChanged(bool value)
    {
        if (!_isLoadingSettings && !value)
        {
            _revokeAudioWrites?.Invoke();
            if (_revokeAudioWrites is not null) _audioWritesRevoked = true;
            if (AutomationEnabled) AutomationEnabled = false;
        }
        PersistSettings();
    }

    partial void OnSelectedTvAudioEndpointChanged(AudioEndpointOption? value) => PersistSettings();

    partial void OnSelectedTvTargetChanged(DisplayTargetOption? value) => PersistSettings();

    partial void OnSelectedInternalTargetChanged(DisplayTargetOption? value) => PersistSettings();

    partial void OnDisplayTargetsErrorChanged(string? value) =>
        OnPropertyChanged(nameof(HasDisplayTargetsError));

    partial void OnIsRefreshingChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayRefreshText));
        OnPropertyChanged(nameof(DisplayRefreshState));
    }

    private void LoadSettings()
    {
        var settings = _settings.Load();

        _isLoadingSettings = true;
        try
        {
            AutomationEnabled = settings.DisplayModeIndex is 0 or 1 && settings.AutomationEnabled;
            CloseLauncherOnActivate = settings.CloseLauncherOnActivate;
            StartWithWindows = settings.StartWithWindows;
            UseWindowsGamepad = settings.UseWindowsGamepad;
            MinimizeToTray = settings.MinimizeToTray;
            ActivationDelayIndex = Math.Clamp(
                settings.ActivationDelayIndex, 0, DelayOptions.Values.Count - 1);
            DeactivationDelayIndex = Math.Clamp(
                settings.DeactivationDelayIndex, 0, DelayOptions.Values.Count - 1);
            DisplayModeIndex = settings.DisplayModeIndex is 0 or 1 ? settings.DisplayModeIndex : 0;
            AudioModeIndex = Math.Clamp(settings.AudioModeIndex, 0, 1);
            SteamEnabled = settings.SteamEnabled;
            LauncherIndex = Math.Clamp(settings.LauncherIndex, 0, 1);
            CustomLauncherPath = settings.CustomLauncherPath;
            AllowDisplayWrites = settings.AllowDisplayWrites;
            AllowAudioWrites = settings.AllowAudioWrites;
            if (!string.IsNullOrWhiteSpace(settings.TvAudioEndpointId))
                SelectedTvAudioEndpoint = new AudioEndpointOption(settings.TvAudioEndpointId, null);

            var tv = CreateSavedTarget(
                settings.TvDevicePath,
                settings.TvEdidManufacturer,
                settings.TvEdidProduct,
                settings.TvExactName);
            var internalDisplay = CreateSavedTarget(
                settings.InternalDevicePath,
                settings.InternalEdidManufacturer,
                settings.InternalEdidProduct,
                settings.InternalExactName);

            if (tv is not null)
            {
                AddTarget(tv);
            }

            if (internalDisplay is not null)
            {
                AddTarget(internalDisplay);
            }

            SelectedTvTarget = tv;
            SelectedInternalTarget = internalDisplay;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private AutomationSettings BuildAutomationSettings() => new()
    {
        Enabled = AutomationEnabled,
        ActivationDelay = DelayOptions.FromIndex(ActivationDelayIndex),
        DeactivationDelay = DelayOptions.FromIndex(DeactivationDelayIndex),
        DisplayMode = DisplayModeIndex switch
        {
            1 => DisplayMode.Extend,
            _ => DisplayMode.TvOnly,
        },
        AudioMode = AudioModeIndex == 1 ? AudioMode.KeepCurrent : AudioMode.MoveToHdmi,
        CloseLauncherOnActivate = CloseLauncherOnActivate,
        LauncherEnabled = SteamEnabled,
        Launcher = LauncherIndex == 1 ? LauncherKind.Hydra : LauncherKind.Steam,
    };

    private void PersistSettings()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        try
        {
            var current = _settings.Load();
            _settings.Save(current with
            {
                AutomationEnabled = AutomationEnabled,
                CloseLauncherOnActivate = CloseLauncherOnActivate,
                StartWithWindows = StartWithWindows,
                UseWindowsGamepad = UseWindowsGamepad,
                MinimizeToTray = MinimizeToTray,
                ActivationDelayIndex = ActivationDelayIndex,
                DeactivationDelayIndex = DeactivationDelayIndex,
                DisplayModeIndex = DisplayModeIndex,
                AudioModeIndex = AudioModeIndex,
                SteamEnabled = SteamEnabled,
                LauncherIndex = LauncherIndex,
                CustomLauncherPath = CustomLauncherPath,
                AllowDisplayWrites = AllowDisplayWrites,
                AllowAudioWrites = AllowAudioWrites,
                TvAudioEndpointId = SelectedTvAudioEndpoint?.Id,
                TvDevicePath = SelectedTvTarget?.DevicePath,
                TvEdidManufacturer = SelectedTvTarget?.EdidManufacturer,
                TvEdidProduct = SelectedTvTarget?.EdidProduct,
                TvExactName = SelectedTvTarget?.FriendlyName,
                InternalDevicePath = SelectedInternalTarget?.DevicePath,
                InternalEdidManufacturer = SelectedInternalTarget?.EdidManufacturer,
                InternalEdidProduct = SelectedInternalTarget?.EdidProduct,
                InternalExactName = SelectedInternalTarget?.FriendlyName,
            });
        }
        catch
        {
            // Preferências não são críticas; a UI continua funcionando.
        }
    }

    private void SyncAutomation()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _ = _coordinator.UpdateSettingsAsync(BuildAutomationSettings());
    }

    private async Task InitializeAutomationAsync()
    {
        await _coordinator.UpdateSettingsAsync(BuildAutomationSettings());

        if (_gamepad.Current is { } device)
        {
            await _coordinator.GamepadConnectedAsync(device);
        }
    }

    private async void OnGamepadConnectionChanged(
        object? sender,
        GamepadConnectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsGamepadConnected));
        OnPropertyChanged(nameof(GamepadSummary));
        OnPropertyChanged(nameof(GamepadActionText));
        OnPropertyChanged(nameof(GamepadTone));

        try
        {
            if (e.Device is null)
            {
                await _coordinator.GamepadDisconnectedAsync();
            }
            else
            {
                await _coordinator.GamepadConnectedAsync(e.Device);
            }
        }
        catch
        {
            // O coordenador já converte falhas em estado Error.
        }
    }

    private void OnAutomationStateChanged(object? sender, AutomationStateChangedEventArgs e)
    {
        void Notify()
        {
            var duration = IsGamepadConnected ? _coordinator.Settings.ActivationDelay : _coordinator.Settings.DeactivationDelay;
            if (e.State == _coordinator.State)
            {
                _countdownTimer.Stop();
                _countdownDeadline = 0;
                if (e.State == AutomationState.Pending)
                {
                    _countdownDeadline = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
                    _countdownTimer.Start();
                }
            }
            _countdownNotifications.StateChanged(e.State, _coordinator.State, duration,
                _localization[IsGamepadConnected ? "notification.activate" : "notification.restore"], _localization["notification.remaining"]);
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CurrentStatusTone));
            OnPropertyChanged(nameof(DisplaySummary));
            OnPropertyChanged(nameof(AudioSummary));
            OnPropertyChanged(nameof(IsRecoveryUncertain));
            OnPropertyChanged(nameof(HasLauncherError));
            OnPropertyChanged(nameof(RecoveryHelpText));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Notify();
        }
        else
        {
            Dispatcher.UIThread.Post(Notify);
        }
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
        OnPropertyChanged(nameof(GamepadSummary));
        OnPropertyChanged(nameof(GamepadActionText));
        OnPropertyChanged(nameof(DisplaySummary));
        OnPropertyChanged(nameof(AudioSummary));
        OnPropertyChanged(nameof(RecoveryHelpText));
        OnPropertyChanged(nameof(LauncherErrorText));
        OnPropertyChanged(nameof(DisplayTargetHint));
        if (DisplayTargetsError is not null)
        {
            DisplayTargetsError = _localization["display.targets_error"];
        }

        OnPropertyChanged(nameof(VersionText));
    }
}

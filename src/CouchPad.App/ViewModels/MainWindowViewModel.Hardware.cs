using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CouchPad.Core.Automation;

namespace CouchPad.App.ViewModels;

public partial class MainWindowViewModel
{
    private readonly Action? _restartForHardware;
    private readonly Action<string>? _playAudioTest;
    private bool _hardwareApplyFailed;
    private bool _hardwareApplied;
    private bool _audioInventoryFailed;
    private string? _audioTestStatus;
    private string? _hardwareSuggestionStatus;

    [ObservableProperty]
    private bool _isApplyingHardware;

    [ObservableProperty]
    private bool _isTestingAudio;

    public event EventHandler? HardwareRestartStarted;
    public bool HasHardwareSuggestion => _hardwareSuggestionStatus is not null;

    public string? HardwareApplyStatus => _hardwareApplyFailed ? _localization["automation.apply_failed"]
        : _hardwareApplied ? _localization["automation.applied"] : null;
    public string? AudioSetupText => _audioTestStatus is { } status ? _localization[status]
        : _audioInventoryFailed ? _localization["audio.inventory_failed"] : null;
    public string? HardwareSuggestionText => _hardwareSuggestionStatus is { } status ? _localization[status] : null;
    public bool SelectedAudioCanAppearLater => SelectedTvAudioEndpoint is { CanAppearAfterDisplayChange: true };

    private bool CanConfirmHardwareSelection => !_audioInventoryFailed &&
        SelectedTvTarget is { IsAvailable: true } tv && DisplayTargets.Any(target => target.Matches(tv) && target.IsAvailable) &&
        SelectedInternalTarget is { IsAvailable: true } monitor && DisplayTargets.Any(target => target.Matches(monitor) && target.IsAvailable) &&
        SelectedTvAudioEndpoint is { } audio && AudioEndpoints.Any(endpoint =>
            string.Equals(endpoint.Id, audio.Id, StringComparison.OrdinalIgnoreCase) &&
            (endpoint.IsAvailable || endpoint.CanAppearAfterDisplayChange));

    private bool CanApplyHardware() => _restartForHardware is not null && ShowHardwareSetup && HasCompleteSelection &&
        CanConfirmHardwareSelection && !HasDisplayTargetsError && !IsApplyingHardware && !IsTestingAudio &&
        _coordinator.State is AutomationState.Idle or AutomationState.Error;

    private bool CanSuggestHardware() => IsProductionMode && !IsApplyingHardware && !AutomationEnabled && !IsRecoveryUncertain;

    [RelayCommand(CanExecute = nameof(CanSuggestHardware))]
    private void SuggestHardware()
    {
        if (!CanSuggestHardware()) return;
        _hardwareSuggestionStatus = null;
        RefreshDisplayTargets();
        RefreshAudioEndpoints();
        var displays = DisplayTargets.Where(target => target.IsAvailable &&
            target.EdidManufacturer.HasValue && target.EdidProduct.HasValue).ToArray();

        // A single active non-HDMI display and a single inactive HDMI display
        // are a useful hint. Two HDMI displays, or more targets, need a choice.
        if (displays.Length == 2 && displays.FirstOrDefault(target => target.IsActive && !target.IsHdmi) is { } monitor &&
            displays.FirstOrDefault(target => !target.IsActive && target.IsHdmi) is { } tv)
        {
            if (SelectedInternalTarget is null && !monitor.Matches(SelectedTvTarget))
                SelectedInternalTarget = monitor;
            if (SelectedTvTarget is null && !tv.Matches(SelectedInternalTarget))
                SelectedTvTarget = tv;
        }

        if (SelectedTvAudioEndpoint is null && !_audioInventoryFailed &&
            SelectedTvTarget?.FriendlyName is { Length: > 0 } name)
        {
            var candidates = AudioEndpoints.Where(endpoint => endpoint.IsDisplayAudio &&
                (endpoint.IsAvailable || endpoint.CanAppearAfterDisplayChange) && endpoint.Name is { } audioName &&
                (audioName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                 audioName.StartsWith(name + " (", StringComparison.OrdinalIgnoreCase) ||
                 audioName.StartsWith(name + " -", StringComparison.OrdinalIgnoreCase))).Take(2).ToArray();
            if (candidates.Length == 1) SelectedTvAudioEndpoint = candidates[0];
            if (candidates.Length > 1) _hardwareSuggestionStatus = "automation.suggest_audio_ambiguous";
        }
        _hardwareSuggestionStatus ??= HasCompleteSelection ? "automation.suggest_review" : "automation.suggest_manual";
        OnPropertyChanged(nameof(HardwareSuggestionText));
        OnPropertyChanged(nameof(HasHardwareSuggestion));
    }

    [RelayCommand(CanExecute = nameof(CanApplyHardware))]
    private async Task ApplyHardwareAsync()
    {
        if (!CanApplyHardware()) return;
        RefreshDisplayTargets();
        RefreshAudioEndpoints();
        if (!CanApplyHardware()) return;
        IsApplyingHardware = true;
        _hardwareApplyFailed = false;
        OnPropertyChanged(nameof(HardwareApplyStatus));
        NotifyHardwareSetup();
        try
        {
            AutomationEnabled = false;
            if (!await PrepareExitAsync() || !HasCompleteSelection || !CanConfirmHardwareSelection || !PersistSettings())
            {
                _hardwareApplyFailed = true;
                return;
            }
            _restartForHardware!();
            HardwareRestartStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _hardwareApplyFailed = true;
        }
        finally
        {
            IsApplyingHardware = false;
            NotifyHardwareSetup();
            OnPropertyChanged(nameof(HardwareApplyStatus));
        }
    }

    private bool CanTestAudio() => _playAudioTest is not null && !IsApplyingHardware && !IsRecoveryUncertain &&
        !IsTestingAudio && !_audioInventoryFailed && SelectedTvAudioEndpoint is { IsAvailable: true } selected &&
        AudioEndpoints.Any(endpoint => endpoint.IsAvailable && string.Equals(endpoint.Id, selected.Id, StringComparison.OrdinalIgnoreCase));

    [RelayCommand(CanExecute = nameof(CanTestAudio))]
    private async Task TestAudioAsync()
    {
        if (!CanTestAudio()) return;
        var selected = SelectedTvAudioEndpoint!;
        IsTestingAudio = true;
        _audioTestStatus = "audio.test_playing";
        NotifyHardwareSetup();
        OnPropertyChanged(nameof(AudioSetupText));
        try
        {
            await Task.Run(() => _playAudioTest!(selected.Id));
            if (SelectedTvAudioEndpoint?.Id == selected.Id) _audioTestStatus = "audio.test_finished";
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TimeoutException)
        {
            if (SelectedTvAudioEndpoint?.Id == selected.Id) _audioTestStatus = "audio.test_failed";
        }
        finally
        {
            IsTestingAudio = false;
            NotifyHardwareSetup();
            OnPropertyChanged(nameof(AudioSetupText));
        }
    }
}

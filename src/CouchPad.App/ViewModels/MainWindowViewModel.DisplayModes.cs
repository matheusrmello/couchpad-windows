using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CouchPad.Core.Display;

namespace CouchPad.App.ViewModels;

/// <summary>
/// Seletores de resolução, taxa de atualização e escala da TV. A preferência é
/// gravada no perfil ativo e espelhada no topo das settings; sem modos
/// enumerados, a preferência existente é preservada (fail-safe).
/// </summary>
public partial class MainWindowViewModel
{
    private IReadOnlyList<DisplayModeOption> _displayModes = [];

    public ObservableCollection<TvModeOption> TvModes { get; } = new();

    [ObservableProperty]
    private TvModeOption? _selectedTvMode;

    public ObservableCollection<TvRefreshOption> TvRefreshOptions { get; } = new();

    [ObservableProperty]
    private TvRefreshOption? _selectedTvRefresh;

    public ObservableCollection<TvScaleOption> TvScaleOptions { get; } = new();

    [ObservableProperty]
    private TvScaleOption? _selectedTvScale;

    [ObservableProperty]
    private string? _tvModesStatus;

    public bool IsTvRefreshEnabled => SelectedTvMode?.Mode is not null && TvRefreshOptions.Count > 0;

    /// <summary>Preferência efetiva das seleções atuais; tudo nulo = automático.</summary>
    public DisplayPreference CurrentDisplayPreference
    {
        get
        {
            if (SelectedTvMode?.Mode is { } mode)
            {
                return new DisplayPreference(
                    mode.Width,
                    mode.Height,
                    SelectedTvRefresh?.Hz,
                    SelectedTvScale?.Percent ?? SelectedEnvironmentProfile?.TvScalePercent);
            }

            if (SelectedEnvironmentProfile is { TvWidth: { } width, TvHeight: { } height } profile)
            {
                return new DisplayPreference(
                    width,
                    height,
                    profile.TvRefreshHz,
                    SelectedTvScale?.Percent ?? profile.TvScalePercent);
            }

            return DisplayPreference.Auto;
        }
    }

    private void LoadDisplayModes()
    {
        var wasLoading = _isLoadingSettings;
        _isLoadingSettings = true;
        try
        {
            var previousMode = SelectedTvMode?.Mode;
            IReadOnlyList<DisplayModeOption> discovered;
            try
            {
                discovered = _discoverDisplayModes?.Invoke() ?? [];
            }
            catch
            {
                discovered = [];
            }

            _displayModes = discovered
                .Where(mode => mode.Width > 0 && mode.Height > 0 && mode.RefreshHz > 0)
                .OrderByDescending(mode => (long)mode.Width * mode.Height)
                .ThenByDescending(mode => mode.RefreshHz)
                .ToArray();

            RebuildTvModeOptions(previousMode);
            RefreshTvRefreshOptions(SelectedTvRefresh?.Hz);
            RebuildTvScaleOptions(SelectedTvScale?.Percent);
        }
        finally
        {
            _isLoadingSettings = wasLoading;
        }
    }

    /// <summary>Uma entrada por resolução (maior Hz representante), área desc.</summary>
    private void RebuildTvModeOptions(DisplayModeOption? previousMode)
    {
        TvModes.Clear();
        TvModes.Add(new TvModeOption(null, _localization["display.tv_auto"]));
        foreach (var resolution in _displayModes
                     .GroupBy(mode => (mode.Width, mode.Height))
                     .Select(group => group.First()))
        {
            TvModes.Add(new TvModeOption(resolution, resolution.ResolutionLabel));
        }

        var match = previousMode is { } previous
            ? TvModes.FirstOrDefault(option => option.Mode is { } mode &&
                mode.Width == previous.Width && mode.Height == previous.Height)
            : null;
        SelectedTvMode = match ?? TvModes[0];
        TvModesStatus = _displayModes.Count == 0 ? _localization["display.tv_mode_unavailable"] : null;
    }

    private void RefreshTvRefreshOptions(int? preferredHz)
    {
        var mode = SelectedTvMode?.Mode;
        TvRefreshOptions.Clear();
        if (mode is not null)
        {
            foreach (var hz in _displayModes
                         .Where(option => option.Width == mode.Width && option.Height == mode.Height)
                         .Select(option => option.RefreshHz)
                         .Distinct()
                         .OrderByDescending(hz => hz))
            {
                TvRefreshOptions.Add(new TvRefreshOption(hz));
            }
        }

        SelectedTvRefresh = preferredHz is { } preferred
            ? TvRefreshOptions.FirstOrDefault(option => option.Hz == preferred) ?? TvRefreshOptions.FirstOrDefault()
            : TvRefreshOptions.FirstOrDefault();
        OnPropertyChanged(nameof(IsTvRefreshEnabled));
    }

    private void RebuildTvScaleOptions(int? preferredPercent)
    {
        TvScaleOptions.Clear();
        TvScaleOptions.Add(new TvScaleOption(null, _localization["display.tv_auto"]));
        foreach (var percent in DisplayPreference.SupportedScalePercents)
        {
            TvScaleOptions.Add(new TvScaleOption(percent, string.Format(_localization["display.tv_scale_value"], percent)));
        }

        SelectedTvScale = preferredPercent is { } preferred
            ? TvScaleOptions.FirstOrDefault(option => option.Percent == preferred) ?? TvScaleOptions[0]
            : TvScaleOptions[0];
    }

    /// <summary>Reaplica os rótulos localizados sem perder as seleções atuais.</summary>
    private void RelocalizeDisplayOptions()
    {
        var wasLoading = _isLoadingSettings;
        _isLoadingSettings = true;
        try
        {
            RebuildTvModeOptions(SelectedTvMode?.Mode);
            RebuildTvScaleOptions(SelectedTvScale?.Percent);
            RefreshTvRefreshOptions(SelectedTvRefresh?.Hz);
        }
        finally
        {
            _isLoadingSettings = wasLoading;
        }
    }

    private void ApplyProfileDisplayPreference(EnvironmentProfileOption profile)
    {
        var match = profile.TvWidth is { } width && profile.TvHeight is { } height
            ? TvModes.FirstOrDefault(option => option.Mode is { } mode &&
                mode.Width == width && mode.Height == height)
            : null;
        SelectedTvMode = match ?? TvModes.FirstOrDefault();
        RefreshTvRefreshOptions(profile.TvRefreshHz);
        SelectedTvScale = TvScaleOptions.FirstOrDefault(option => option.Percent == profile.TvScalePercent)
            ?? TvScaleOptions.FirstOrDefault();
    }

    private void SyncSelectedProfileDisplayPreference(EnvironmentProfileOption profile)
    {
        // Resolução/refresh só são gravados quando há um modo enumerado
        // selecionado; sem enumeração o perfil mantém o que já tinha.
        if (SelectedTvMode?.Mode is { } mode)
        {
            profile.TvWidth = mode.Width;
            profile.TvHeight = mode.Height;
            profile.TvRefreshHz = SelectedTvRefresh?.Hz;
        }

        // A escala é gravada sempre, mesmo sem modos enumerados.
        profile.TvScalePercent = SelectedTvScale?.Percent ?? profile.TvScalePercent;
    }

    private void ResetDisplaySelections()
    {
        SelectedTvMode = TvModes.FirstOrDefault(option => option.Mode is null);
        TvRefreshOptions.Clear();
        SelectedTvRefresh = null;
        SelectedTvScale = TvScaleOptions.FirstOrDefault(option => option.Percent is null);
    }

    partial void OnSelectedTvModeChanged(TvModeOption? value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        var wasLoading = _isLoadingSettings;
        _isLoadingSettings = true;
        try
        {
            RefreshTvRefreshOptions(SelectedTvRefresh?.Hz);
        }
        finally
        {
            _isLoadingSettings = wasLoading;
        }

        SyncSelectedProfileFromSelection();
        PersistSettings();
    }

    partial void OnSelectedTvRefreshChanged(TvRefreshOption? value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        SyncSelectedProfileFromSelection();
        PersistSettings();
    }

    partial void OnSelectedTvScaleChanged(TvScaleOption? value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        SyncSelectedProfileFromSelection();
        PersistSettings();
    }
}

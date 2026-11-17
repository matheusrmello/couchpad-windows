using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CouchPad.App.Localization;
using CouchPad.Core.Devices;
using CouchPad.Core.Performance;

namespace CouchPad.App.ViewModels;

public sealed record HudChordOption(string Key, string Label);

public sealed record HudPositionOption(string Key, string Label);

public partial class PerformanceHudSettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _cpuSelected = true;
    [ObservableProperty] private bool _cpuProcessSelected;
    [ObservableProperty] private bool _gpuSelected = true;
    [ObservableProperty] private bool _ramSelected = true;
    [ObservableProperty] private bool _vramSelected;
    [ObservableProperty] private bool _processSelected = true;
    [ObservableProperty] private bool _displaySelected;
    [ObservableProperty] private double _opacity;
    [ObservableProperty] private double _scale;
    [ObservableProperty] private bool _showOnlyInSession;
    [ObservableProperty] private HudChordOption? _selectedChord;
    [ObservableProperty] private HudPositionOption? _selectedPosition;

    public ObservableCollection<HudChordOption> ChordOptions { get; }
    public ObservableCollection<HudPositionOption> PositionOptions { get; }

    public bool ShowEmptyPreview => !(CpuSelected || CpuProcessSelected || GpuSelected ||
        RamSelected || VramSelected || ProcessSelected || DisplaySelected);

    partial void OnCpuSelectedChanged(bool value) => NotifyPreview();
    partial void OnCpuProcessSelectedChanged(bool value) => NotifyPreview();
    partial void OnGpuSelectedChanged(bool value) => NotifyPreview();
    partial void OnRamSelectedChanged(bool value) => NotifyPreview();
    partial void OnVramSelectedChanged(bool value) => NotifyPreview();
    partial void OnProcessSelectedChanged(bool value) => NotifyPreview();
    partial void OnDisplaySelectedChanged(bool value) => NotifyPreview();

    public PerformanceHudSettingsViewModel(PerformanceHudSettings settings)
    {
        var localization = LocalizationService.Instance;
        ChordOptions = new(GamepadChordPresets.All.Select(preset =>
            new HudChordOption(preset.Key, localization[HudText.ChordKey(preset.Key)])));
        PositionOptions = new(
        [
            new("topLeft", localization["hud.position.top_left"]),
            new("topRight", localization["hud.position.top_right"]),
            new("bottomLeft", localization["hud.position.bottom_left"]),
            new("bottomRight", localization["hud.position.bottom_right"]),
        ]);

        Enabled = settings.Enabled;
        CpuSelected = settings.Metrics.Contains(PerformanceMetric.Cpu);
        CpuProcessSelected = settings.Metrics.Contains(PerformanceMetric.CpuProcess);
        GpuSelected = settings.Metrics.Contains(PerformanceMetric.Gpu);
        RamSelected = settings.Metrics.Contains(PerformanceMetric.Ram);
        VramSelected = settings.Metrics.Contains(PerformanceMetric.Vram);
        ProcessSelected = settings.Metrics.Contains(PerformanceMetric.Process);
        DisplaySelected = settings.Metrics.Contains(PerformanceMetric.Display);
        Opacity = settings.Opacity;
        Scale = settings.Scale;
        ShowOnlyInSession = settings.ShowOnlyInSession;
        SelectedChord = ChordOptions.FirstOrDefault(option => option.Key == settings.ChordKey) ?? ChordOptions[0];
        SelectedPosition = PositionOptions.FirstOrDefault(option =>
            option.Key == PerformanceHudPositionCatalog.Key(settings.Position)) ?? PositionOptions[1];
    }

    public PerformanceHudSettings ToSettings()
    {
        var metrics = new List<PerformanceMetric>();
        if (CpuSelected) metrics.Add(PerformanceMetric.Cpu);
        if (CpuProcessSelected) metrics.Add(PerformanceMetric.CpuProcess);
        if (GpuSelected) metrics.Add(PerformanceMetric.Gpu);
        if (RamSelected) metrics.Add(PerformanceMetric.Ram);
        if (VramSelected) metrics.Add(PerformanceMetric.Vram);
        if (ProcessSelected) metrics.Add(PerformanceMetric.Process);
        if (DisplaySelected) metrics.Add(PerformanceMetric.Display);
        if (metrics.Count == 0) metrics.AddRange(PerformanceMetricCatalog.DefaultMetrics);

        return new PerformanceHudSettings(
            Enabled,
            metrics,
            PerformanceHudPositionCatalog.Parse(SelectedPosition?.Key),
            Math.Clamp(Opacity, 0.4, 1.0),
            Math.Clamp(Scale, 0.75, 1.5),
            ShowOnlyInSession,
            SelectedChord?.Key ?? GamepadChordPresets.DefaultKey);
    }

    private void NotifyPreview() => OnPropertyChanged(nameof(ShowEmptyPreview));
}

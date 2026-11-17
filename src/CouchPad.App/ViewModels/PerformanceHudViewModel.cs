using CommunityToolkit.Mvvm.ComponentModel;
using CouchPad.Core.Performance;

namespace CouchPad.App.ViewModels;

public partial class PerformanceHudViewModel : ObservableObject
{
    private const string Unavailable = "—";
    private const double BarMax = 110;

    [ObservableProperty] private bool _showCpu;
    [ObservableProperty] private bool _showCpuProcess;
    [ObservableProperty] private bool _showGpu;
    [ObservableProperty] private bool _showRam;
    [ObservableProperty] private bool _showVram;
    [ObservableProperty] private bool _showProcess;
    [ObservableProperty] private bool _showDisplay;

    [ObservableProperty] private string _cpuText = Unavailable;
    [ObservableProperty] private string _cpuGameText = Unavailable;
    [ObservableProperty] private string _gpuText = Unavailable;
    [ObservableProperty] private string _ramText = Unavailable;
    [ObservableProperty] private string _vramText = Unavailable;
    [ObservableProperty] private string _processText = Unavailable;
    [ObservableProperty] private string _displayText = Unavailable;

    [ObservableProperty] private double _cpuBarWidth;
    [ObservableProperty] private double _gpuBarWidth;
    [ObservableProperty] private double _ramBarWidth;
    [ObservableProperty] private double _vramBarWidth;

    public bool HasMetrics => ShowCpu || ShowCpuProcess || ShowGpu || ShowRam || ShowVram || ShowProcess || ShowDisplay;

    public bool ShowEmpty => !HasMetrics;

    public void ApplySettings(PerformanceHudSettings settings)
    {
        ShowCpu = settings.Metrics.Contains(PerformanceMetric.Cpu);
        ShowCpuProcess = settings.Metrics.Contains(PerformanceMetric.CpuProcess);
        ShowGpu = settings.Metrics.Contains(PerformanceMetric.Gpu);
        ShowRam = settings.Metrics.Contains(PerformanceMetric.Ram);
        ShowVram = settings.Metrics.Contains(PerformanceMetric.Vram);
        ShowProcess = settings.Metrics.Contains(PerformanceMetric.Process);
        ShowDisplay = settings.Metrics.Contains(PerformanceMetric.Display);
        OnPropertyChanged(nameof(HasMetrics));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    public void Update(PerformanceSnapshot snapshot)
    {
        CpuText = FormatPercent(snapshot.CpuTotalPercent);
        CpuGameText = FormatPercent(snapshot.CpuProcessPercent);
        GpuText = FormatPercent(snapshot.GpuPercent);
        RamText = FormatBytes(snapshot.MemoryUsedBytes, snapshot.MemoryTotalBytes);
        VramText = FormatBytes(snapshot.VramUsedBytes, snapshot.VramTotalBytes);
        ProcessText = string.IsNullOrWhiteSpace(snapshot.ForegroundProcessName)
            ? Unavailable
            : snapshot.ForegroundProcessName;
        DisplayText = FormatDisplay(snapshot.ScreenWidth, snapshot.ScreenHeight, snapshot.RefreshHz);

        CpuBarWidth = BarWidth(snapshot.CpuTotalPercent);
        GpuBarWidth = BarWidth(snapshot.GpuPercent);
        RamBarWidth = RatioBar(snapshot.MemoryUsedBytes, snapshot.MemoryTotalBytes);
        VramBarWidth = RatioBar(snapshot.VramUsedBytes, snapshot.VramTotalBytes);
    }

    private static string FormatPercent(double? value) => value is { } percent ? $"{percent:0}%" : Unavailable;

    private static string FormatBytes(ulong? used, ulong? total)
    {
        if (used is null) return Unavailable;
        var usedGb = used.Value / (1024d * 1024 * 1024);
        if (total is null) return $"{usedGb:0.0} GB";
        var totalGb = total.Value / (1024d * 1024 * 1024);
        return $"{usedGb:0.0} / {totalGb:0.0} GB";
    }

    private static string FormatDisplay(int? width, int? height, int? refresh)
    {
        if (width is null || height is null) return Unavailable;
        return refresh is > 0 ? $"{width}×{height} · {refresh} Hz" : $"{width}×{height}";
    }

    private static double BarWidth(double? percent) =>
        percent is { } value ? Math.Clamp(value, 0, 100) / 100 * BarMax : 0;

    private static double RatioBar(ulong? used, ulong? total) =>
        used is { } value && total is > 0 && total is { } max
            ? Math.Clamp((double)value / max, 0, 1) * BarMax
            : 0;
}

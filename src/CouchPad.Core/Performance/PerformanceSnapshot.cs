namespace CouchPad.Core.Performance;

/// <summary>Read-only performance sample for the local HUD.</summary>
public sealed record PerformanceSnapshot(
    double? CpuTotalPercent,
    double? CpuProcessPercent,
    double? GpuPercent,
    ulong? MemoryUsedBytes,
    ulong? MemoryTotalBytes,
    ulong? VramUsedBytes,
    ulong? VramTotalBytes,
    string? ForegroundProcessName,
    int? ScreenWidth,
    int? ScreenHeight,
    int? RefreshHz,
    DateTimeOffset CapturedAt)
{
    public static PerformanceSnapshot Empty { get; } = new(
        null, null, null, null, null, null, null, null, null, null, null, DateTimeOffset.MinValue);
}

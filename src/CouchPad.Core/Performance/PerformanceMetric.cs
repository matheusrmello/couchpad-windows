namespace CouchPad.Core.Performance;

public enum PerformanceMetric
{
    Cpu,
    CpuProcess,
    Gpu,
    Ram,
    Vram,
    Process,
    Display,
    FramesPerSecond,
}

public static class PerformanceMetricCatalog
{
    public static IReadOnlyList<PerformanceMetric> DefaultMetrics { get; } =
        [PerformanceMetric.Cpu, PerformanceMetric.Gpu, PerformanceMetric.Ram, PerformanceMetric.Process];

    public static IReadOnlyList<PerformanceMetric> All { get; } =
    [
        PerformanceMetric.Cpu,
        PerformanceMetric.CpuProcess,
        PerformanceMetric.Gpu,
        PerformanceMetric.Ram,
        PerformanceMetric.Vram,
        PerformanceMetric.Process,
        PerformanceMetric.Display,
        PerformanceMetric.FramesPerSecond,
    ];

    public static string Key(PerformanceMetric metric) => metric switch
    {
        PerformanceMetric.Cpu => "cpu",
        PerformanceMetric.CpuProcess => "cpuProcess",
        PerformanceMetric.Gpu => "gpu",
        PerformanceMetric.Ram => "ram",
        PerformanceMetric.Vram => "vram",
        PerformanceMetric.Process => "process",
        PerformanceMetric.Display => "display",
        PerformanceMetric.FramesPerSecond => "fps",
        _ => throw new ArgumentOutOfRangeException(nameof(metric)),
    };

    public static bool TryParse(string? key, out PerformanceMetric metric)
    {
        switch (key?.Trim().ToLowerInvariant())
        {
            case "cpu": metric = PerformanceMetric.Cpu; return true;
            case "cpuprocess" or "cpu_process": metric = PerformanceMetric.CpuProcess; return true;
            case "gpu": metric = PerformanceMetric.Gpu; return true;
            case "ram": metric = PerformanceMetric.Ram; return true;
            case "vram": metric = PerformanceMetric.Vram; return true;
            case "process": metric = PerformanceMetric.Process; return true;
            case "display": metric = PerformanceMetric.Display; return true;
            case "fps" or "framerate": metric = PerformanceMetric.FramesPerSecond; return true;
            default: metric = default; return false;
        }
    }

    public static IReadOnlyList<PerformanceMetric> Parse(IEnumerable<string>? keys)
    {
        if (keys is null) return DefaultMetrics;
        var result = new List<PerformanceMetric>();
        foreach (var key in keys)
        {
            if (TryParse(key, out var metric) && !result.Contains(metric))
                result.Add(metric);
        }
        return result.Count == 0 ? DefaultMetrics : result;
    }

    public static IReadOnlyList<string> ToKeys(IEnumerable<PerformanceMetric> metrics) =>
        metrics.Distinct().Select(Key).ToArray();
}

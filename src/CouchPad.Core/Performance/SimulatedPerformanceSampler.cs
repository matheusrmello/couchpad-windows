namespace CouchPad.Core.Performance;

/// <summary>Deterministic local sampler used by simulation and UI tests.</summary>
public sealed class SimulatedPerformanceSampler : IPerformanceSampler
{
    private int _tick;

    public Task<PerformanceSnapshot> SampleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tick = Interlocked.Increment(ref _tick);
        var cpu = 24 + (tick * 7 % 28);
        var processCpu = 8 + (tick * 3 % 16);
        var gpu = 62 + (tick * 11 % 32);
        var memoryUsed = (ulong)(12.4 * 1024 * 1024 * 1024);
        var memoryTotal = 32UL * 1024 * 1024 * 1024;
        var vramUsed = (ulong)(7.2 * 1024 * 1024 * 1024);
        var vramTotal = 12UL * 1024 * 1024 * 1024;
        var fps = 58 + (tick * 7 % 62);
        return Task.FromResult(new PerformanceSnapshot(
            cpu,
            processCpu,
            gpu,
            memoryUsed,
            memoryTotal,
            vramUsed,
            vramTotal,
            "CouchPad (simulação)",
            1920,
            1080,
            120,
            DateTimeOffset.UtcNow,
            fps));
    }
}

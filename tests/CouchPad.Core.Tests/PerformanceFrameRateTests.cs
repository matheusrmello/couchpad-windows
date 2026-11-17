using CouchPad.Core.Performance;

namespace CouchPad.Core.Tests;

public sealed class PerformanceMetricFrameRateTests
{
    [Theory]
    [InlineData("fps")]
    [InlineData("framerate")]
    [InlineData("FPS")]
    public void Catalog_parses_frame_rate_aliases(string key)
    {
        var metrics = PerformanceMetricCatalog.Parse([key]);

        Assert.Equal([PerformanceMetric.FramesPerSecond], metrics);
    }

    [Fact]
    public void Catalog_exposes_fps_without_enabling_it_by_default()
    {
        Assert.Equal("fps", PerformanceMetricCatalog.Key(PerformanceMetric.FramesPerSecond));
        Assert.Contains(PerformanceMetric.FramesPerSecond, PerformanceMetricCatalog.All);
        Assert.DoesNotContain(PerformanceMetric.FramesPerSecond, PerformanceMetricCatalog.DefaultMetrics);
    }

    [Fact]
    public void Catalog_deduplicates_frame_rate_keys()
    {
        var metrics = PerformanceMetricCatalog.Parse(["fps", "framerate", "FPS", "cpu"]);

        Assert.Equal([PerformanceMetric.FramesPerSecond, PerformanceMetric.Cpu], metrics);
        Assert.Equal(
            ["fps"],
            PerformanceMetricCatalog.ToKeys([PerformanceMetric.FramesPerSecond, PerformanceMetric.FramesPerSecond]));
    }

    [Fact]
    public void Snapshot_accepts_trailing_frames_per_second()
    {
        var captured = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new PerformanceSnapshot(
            10, 5, 40, 1, 2, 3, 4, "game", 1920, 1080, 120, captured, 144);

        Assert.Equal(144d, snapshot.FramesPerSecond);
        Assert.Equal(captured, snapshot.CapturedAt);
        Assert.Null(PerformanceSnapshot.Empty.FramesPerSecond);
    }

    [Fact]
    public async Task Simulated_sampler_reports_varying_plausible_fps()
    {
        var sampler = new SimulatedPerformanceSampler();

        var first = await sampler.SampleAsync();
        var second = await sampler.SampleAsync();

        Assert.NotNull(first.FramesPerSecond);
        Assert.NotNull(second.FramesPerSecond);
        Assert.InRange(first.FramesPerSecond!.Value, 58, 120);
        Assert.InRange(second.FramesPerSecond!.Value, 58, 120);
        Assert.NotEqual(first.FramesPerSecond, second.FramesPerSecond);
    }
}

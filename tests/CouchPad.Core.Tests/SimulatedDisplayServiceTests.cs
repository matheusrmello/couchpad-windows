using CouchPad.Core.Display;

namespace CouchPad.Core.Tests;

public sealed class SimulatedDisplayServiceTests
{
    [Fact]
    public void Baseline_is_internal_only()
    {
        var service = new SimulatedDisplayService(TimeSpan.Zero);

        var current = service.GetCurrent();

        Assert.Equal(DisplayMode.InternalOnly, current.Mode);
        Assert.True(current.PrimaryActive);
        Assert.False(current.TvActive);
    }

    [Fact]
    public void Available_modes_are_not_empty_and_include_1080p60()
    {
        var service = new SimulatedDisplayService(TimeSpan.Zero);

        var modes = service.GetAvailableModes();

        Assert.NotEmpty(modes);
        Assert.Contains(new DisplayModeOption(1920, 1080, 60), modes);
    }

    [Fact]
    public async Task Apply_modes_map_to_expected_state()
    {
        var service = new SimulatedDisplayService(TimeSpan.Zero);

        var tvOnly = await service.ApplyAsync(DisplayMode.TvOnly);
        Assert.Equal(DisplayMode.TvOnly, tvOnly.Mode);
        Assert.False(tvOnly.PrimaryActive);
        Assert.True(tvOnly.TvActive);

        var internalOnly = await service.ApplyAsync(DisplayMode.InternalOnly);
        Assert.Equal(DisplayMode.InternalOnly, internalOnly.Mode);
        Assert.True(internalOnly.PrimaryActive);
        Assert.False(internalOnly.TvActive);

        var extend = await service.ApplyAsync(DisplayMode.Extend);
        Assert.Equal(DisplayMode.Extend, extend.Mode);
        Assert.True(extend.PrimaryActive);
        Assert.True(extend.TvActive);

        var error = await Assert.ThrowsAsync<NotSupportedException>(() => service.ApplyAsync(DisplayMode.Mirror));
        Assert.True(DisplayApplyFailure.IsUnchanged(error));
    }

    [Fact]
    public async Task Restore_returns_to_the_received_baseline()
    {
        var service = new SimulatedDisplayService(TimeSpan.Zero);
        var baseline = new DisplayState(DisplayMode.InternalOnly, PrimaryActive: true, TvActive: false)
        {
            Topology = "baseline",
        };

        await service.ApplyAsync(DisplayMode.TvOnly);
        await service.RestoreAsync(baseline);

        Assert.Equal(baseline, service.GetCurrent());
    }

    [Fact]
    public async Task Mirrored_baseline_can_still_be_restored()
    {
        var service = new SimulatedDisplayService(TimeSpan.Zero);
        var baseline = new DisplayState(DisplayMode.Mirror, true, true) { Topology = "baseline" };
        await service.RestoreAsync(baseline);
        Assert.Equal(baseline, service.GetCurrent());
    }
}

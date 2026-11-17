using CouchPad.Core.Devices;
using CouchPad.Core.Performance;
using CouchPad.Core.Settings;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class GamepadChordDetectorTests
{
    private static readonly GamepadButtons Chord = GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.Y;
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Fires_once_after_hold_and_rearms_after_release()
    {
        var detector = new GamepadChordDetector(new GamepadChord(Chord, TimeSpan.FromMilliseconds(800)), TimeSpan.FromSeconds(1));

        Assert.False(detector.Update(GamepadButtons.LeftShoulder, Start));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(100)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(700)));
        Assert.True(detector.Update(Chord, Start.AddMilliseconds(900)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(1000)));

        Assert.False(detector.Update(GamepadButtons.None, Start.AddMilliseconds(1050)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(1100)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(1800)));
        Assert.True(detector.Update(Chord, Start.AddMilliseconds(2000)));
    }

    [Fact]
    public void Resets_when_chord_is_partial()
    {
        var detector = new GamepadChordDetector(new GamepadChord(Chord, TimeSpan.FromMilliseconds(500)));

        Assert.False(detector.Update(Chord, Start));
        Assert.False(detector.Update(GamepadButtons.LeftShoulder | GamepadButtons.Y, Start.AddMilliseconds(400)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(600)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(700)));
    }

    [Fact]
    public void Rejects_extra_buttons_and_reset_clears_state()
    {
        var detector = new GamepadChordDetector(new GamepadChord(Chord, TimeSpan.FromMilliseconds(500)));
        detector.Update(Chord, Start);

        Assert.False(detector.Update(Chord | GamepadButtons.A, Start.AddMilliseconds(900)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(1000)));

        detector.Reset();
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(2000)));
        Assert.False(detector.Update(Chord, Start.AddMilliseconds(2400)));
        Assert.True(detector.Update(Chord, Start.AddMilliseconds(2500)));
    }
}

public sealed class PerformanceHudSettingsTests
{
    [Fact]
    public void Legacy_settings_use_defaults()
    {
        var settings = PerformanceHudSettings.FromAppSettings(new AppSettings());

        Assert.False(settings.Enabled);
        Assert.Equal(PerformanceMetricCatalog.DefaultMetrics, settings.Metrics);
        Assert.Equal(PerformanceHudPosition.TopRight, settings.Position);
        Assert.Equal(0.9, settings.Opacity);
        Assert.Equal(1.0, settings.Scale);
        Assert.True(settings.ShowOnlyInSession);
        Assert.Equal(GamepadChordPresets.DefaultKey, settings.ChordKey);
    }

    [Fact]
    public void Roundtrip_preserves_hud_fields()
    {
        var settings = new PerformanceHudSettings(
            true,
            [PerformanceMetric.Cpu, PerformanceMetric.Vram, PerformanceMetric.Display],
            PerformanceHudPosition.BottomLeft,
            0.55,
            1.25,
            false,
            "LB+RB+Back");

        var app = settings.ApplyTo(new AppSettings());
        var actual = PerformanceHudSettings.FromAppSettings(app);

        Assert.Equal(settings.Enabled, actual.Enabled);
        Assert.Equal(settings.Metrics, actual.Metrics);
        Assert.Equal(settings.Position, actual.Position);
        Assert.Equal(settings.Opacity, actual.Opacity);
        Assert.Equal(settings.Scale, actual.Scale);
        Assert.Equal(settings.ShowOnlyInSession, actual.ShowOnlyInSession);
        Assert.Equal(settings.ChordKey, actual.ChordKey);
    }

    [Fact]
    public void Invalid_values_fall_back_or_clamp()
    {
        var app = new AppSettings
        {
            PerformanceHudMetrics = ["unknown", "cpu"],
            PerformanceHudPosition = "invalid",
            PerformanceHudOpacity = 0.1,
            PerformanceHudScale = 4,
            PerformanceHudToggleChord = "invalid",
        };

        var settings = PerformanceHudSettings.FromAppSettings(app);

        Assert.Equal([PerformanceMetric.Cpu], settings.Metrics);
        Assert.Equal(PerformanceHudPosition.TopRight, settings.Position);
        Assert.Equal(0.4, settings.Opacity);
        Assert.Equal(1.5, settings.Scale);
        Assert.Equal(GamepadChordPresets.DefaultKey, settings.ChordKey);
    }

    [Fact]
    public void Metric_catalog_parses_and_deduplicates()
    {
        var metrics = PerformanceMetricCatalog.Parse(["gpu", "GPU", "cpuProcess", "bad", "ram"]);

        Assert.Equal([PerformanceMetric.Gpu, PerformanceMetric.CpuProcess, PerformanceMetric.Ram], metrics);
        Assert.Equal(["gpu", "ram"], PerformanceMetricCatalog.ToKeys([PerformanceMetric.Gpu, PerformanceMetric.Ram, PerformanceMetric.Gpu]));
    }
}

public sealed class WindowsGamepadButtonSourceTests
{
    [Fact]
    public void Maps_xinput_buttons_and_ignores_unmapped_buttons()
    {
        var mapped = WindowsGamepadButtonSource.Map(0x1000 | 0x0100 | 0x2000);

        Assert.True(mapped.HasFlag(GamepadButtons.A));
        Assert.True(mapped.HasFlag(GamepadButtons.LeftShoulder));
        Assert.True(mapped.HasFlag(GamepadButtons.B));
        Assert.False(mapped.HasFlag(GamepadButtons.Start));
        Assert.Equal(GamepadButtons.None, WindowsGamepadButtonSource.Map(0x0001));
    }

    [Fact]
    public async Task Reports_repeated_state_on_every_poll_tick()
    {
        if (!OperatingSystem.IsWindows()) return;

        var source = new WindowsGamepadButtonSource(new HeldChordXInputStateSource(), TimeSpan.FromMilliseconds(10));
        var reports = 0;
        try
        {
            source.ButtonsReported += (_, e) =>
            {
                if (e.Buttons == (GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.Y))
                    Interlocked.Increment(ref reports);
            };

            await Task.Delay(300);

            Assert.True(reports >= 2, $"Expected repeated reports while the chord stays held, got {reports}.");
        }
        finally
        {
            source.Dispose();
        }
    }

    private sealed class HeldChordXInputStateSource : IXInputStateSource
    {
        public IReadOnlyList<uint> GetConnectedSlots() => [0];

        public string? GetModelName(uint slot) => "fake";

        public bool TryGetButtons(uint slot, out ushort buttons)
        {
            buttons = 0x8300;
            return true;
        }
    }
}

public sealed class SimulatedPerformanceSamplerTests
{
    [Fact]
    public async Task Produces_plausible_varying_values()
    {
        var sampler = new SimulatedPerformanceSampler();

        var first = await sampler.SampleAsync();
        var second = await sampler.SampleAsync();

        Assert.NotNull(first.CpuTotalPercent);
        Assert.NotNull(second.CpuTotalPercent);
        Assert.NotEqual(first.CpuTotalPercent, second.CpuTotalPercent);
        Assert.Equal(1920, first.ScreenWidth);
        Assert.Equal(1080, first.ScreenHeight);
        Assert.Equal(120, first.RefreshHz);
        Assert.False(string.IsNullOrWhiteSpace(first.ForegroundProcessName));
    }
}

using CouchPad.Core.Display;

namespace CouchPad.Core.Tests;

public sealed class DisplayPreferenceTests
{
    [Fact]
    public void Auto_is_auto()
    {
        Assert.True(DisplayPreference.Auto.IsAuto);
        Assert.True(DisplayPreference.Auto.IsValid);
        Assert.Equal(DisplayPreference.Auto, DisplayPreference.Auto.Normalize());
    }

    [Fact]
    public void Fully_specified_preference_is_not_auto_and_is_valid()
    {
        var preference = new DisplayPreference(1920, 1080, 120, 150);

        Assert.False(preference.IsAuto);
        Assert.True(preference.IsValid);
    }

    [Theory]
    [InlineData(null, 1080)]
    [InlineData(1920, null)]
    [InlineData(0, 1080)]
    [InlineData(1920, -1)]
    public void Incomplete_or_non_positive_resolution_is_invalid(int? width, int? height) =>
        Assert.False(new DisplayPreference(width, height, null, null).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void Non_positive_refresh_is_invalid(int refreshHz) =>
        Assert.False(new DisplayPreference(null, null, refreshHz, null).IsValid);

    [Theory]
    [InlineData(99)]
    [InlineData(110)]
    [InlineData(250)]
    public void Scale_outside_supported_set_is_invalid(int scalePercent) =>
        Assert.False(new DisplayPreference(null, null, null, scalePercent).IsValid);

    [Fact]
    public void Normalize_keeps_fully_valid_preference()
    {
        var preference = new DisplayPreference(2560, 1440, 60, 125);

        Assert.Equal(preference, preference.Normalize());
    }

    [Fact]
    public void Normalize_drops_incomplete_resolution()
    {
        var normalized = new DisplayPreference(1920, null, 60, 100).Normalize();

        Assert.Null(normalized.Width);
        Assert.Null(normalized.Height);
        Assert.Equal(60, normalized.RefreshHz);
        Assert.Equal(100, normalized.ScalePercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Normalize_drops_invalid_refresh(int refreshHz) =>
        Assert.Null(new DisplayPreference(1920, 1080, refreshHz, null).Normalize().RefreshHz);

    [Theory]
    [InlineData(99)]
    [InlineData(110)]
    [InlineData(250)]
    public void Normalize_drops_scale_outside_100_to_200(int scalePercent) =>
        Assert.Null(new DisplayPreference(1920, 1080, 60, scalePercent).Normalize().ScalePercent);

    [Fact]
    public void Supported_scale_percents_match_windows_set() =>
        Assert.Equal([100, 125, 150, 175, 200], DisplayPreference.SupportedScalePercents);
}

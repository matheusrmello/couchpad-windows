using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.Core.Display;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

/// <summary>
/// Etapa 2b: a lista de modos da TV marca o que o EDID não anuncia. A marca é
/// apenas visual — a lógica pura fica em <see cref="TvModeAdvertisement"/>.
/// </summary>
[Collection("Localization")]
public sealed class TvModeAdvertisementTests
{
    [Fact]
    public void Exact_advertised_mode_is_not_marked()
    {
        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(1920, 1080, 60)],
            [new DisplayModeOption(1920, 1080, 60)],
            "não anunciado pela TV");

        Assert.Equal("1920×1080", Assert.Single(labels).Label);
    }

    [Fact]
    public void Refresh_within_one_hertz_is_not_marked()
    {
        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(1920, 1080, 60)],
            [new DisplayModeOption(1920, 1080, 61)],
            "não anunciado pela TV");

        Assert.Equal("1920×1080", Assert.Single(labels).Label);
    }

    [Fact]
    public void Resolution_is_not_marked_when_any_of_its_rates_is_advertised()
    {
        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(1920, 1080, 120), new DisplayModeOption(1920, 1080, 60)],
            [new DisplayModeOption(1920, 1080, 59)],
            "não anunciado pela TV");

        Assert.Equal("1920×1080", Assert.Single(labels).Label);
    }

    [Fact]
    public void Refresh_two_hertz_apart_without_other_advertised_rate_is_marked()
    {
        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(1920, 1080, 60)],
            [new DisplayModeOption(1920, 1080, 62)],
            "não anunciado pela TV");

        Assert.Equal("1920×1080 — não anunciado pela TV", Assert.Single(labels).Label);
    }

    [Fact]
    public void Resolution_missing_from_advertised_set_is_marked()
    {
        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(2560, 1440, 144)],
            [new DisplayModeOption(1920, 1080, 60)],
            "não anunciado pela TV");

        Assert.Equal("2560×1440 — não anunciado pela TV", Assert.Single(labels).Label);
    }

    [Fact]
    public void Empty_advertised_list_marks_nothing()
    {
        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(3840, 2160, 30), new DisplayModeOption(1920, 1080, 60)],
            [],
            "não anunciado pela TV");

        Assert.All(labels, option => Assert.DoesNotContain("—", option.Label, StringComparison.Ordinal));
    }

    [Fact]
    public void Portuguese_label_matches_the_catalog()
    {
        var localization = LocalizationService.Instance;
        localization.Initialize(new FakeSettingsStore { Current = new AppSettings { Language = "pt-BR" } });

        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(1920, 1080, 60)],
            [new DisplayModeOption(2560, 1440, 60)],
            localization["display.mode_not_advertised"]);

        Assert.Equal("1920×1080 — não anunciado pela TV", Assert.Single(labels).Label);
    }

    [Fact]
    public void English_label_matches_the_catalog()
    {
        var localization = LocalizationService.Instance;
        localization.Initialize(new FakeSettingsStore { Current = new AppSettings { Language = "en" } });

        var labels = TvModeAdvertisement.BuildTvModeLabels(
            [new DisplayModeOption(1920, 1080, 60)],
            [new DisplayModeOption(2560, 1440, 60)],
            localization["display.mode_not_advertised"]);

        Assert.Equal("1920×1080 — not advertised by the TV", Assert.Single(labels).Label);
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public AppSettings Current { get; set; } = new();

        public AppSettings Load() => Current;

        public void Save(AppSettings settings) => Current = settings;
    }
}

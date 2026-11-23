using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

/// <summary>
/// P4A: seletores de resolução/refresh/escala da TV refletem o perfil ativo,
/// são persistidos em AppSettings e preservados quando a enumeração falha.
/// </summary>
[Collection("Localization")]
public sealed class DisplayPreferenceSelectionTests
{
    private static readonly DisplayModeOption[] DiscoveredModes =
    [
        new(1920, 1080, 60),
        new(1920, 1080, 120),
        new(3840, 2160, 30),
    ];

    [Fact]
    public void Selecting_mode_refresh_and_scale_updates_preference_and_persists()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings
            {
                Language = "pt-BR",
                ActiveProfileId = "sala",
                EnvironmentProfiles = [new EnvironmentProfile { Id = "sala", Name = "Sala" }],
            },
        };
        LocalizationService.Instance.Initialize(store);

        var vm = CreateViewModel(store, DiscoveredModes);

        Assert.Equal(DisplayPreference.Auto, vm.CurrentDisplayPreference);

        vm.SelectedTvMode = Assert.Single(vm.TvModes,
            option => option.Mode is { Width: 1920, Height: 1080 });
        vm.SelectedTvRefresh = Assert.Single(vm.TvRefreshOptions, option => option.Hz == 120);
        vm.SelectedTvScale = Assert.Single(vm.TvScaleOptions, option => option.Percent == 125);

        Assert.Equal(new DisplayPreference(1920, 1080, 120, 125), vm.CurrentDisplayPreference);

        vm.MinimizeToTray = !vm.MinimizeToTray;

        Assert.Equal(1920, store.Current.TvWidth);
        Assert.Equal(1080, store.Current.TvHeight);
        Assert.Equal(120, store.Current.TvRefreshHz);
        Assert.Equal(125, store.Current.TvScalePercent);
        var profile = Assert.Single(store.Current.EnvironmentProfiles!);
        Assert.Equal(1920, profile.TvWidth);
        Assert.Equal(1080, profile.TvHeight);
        Assert.Equal(120, profile.TvRefreshHz);
        Assert.Equal(125, profile.TvScalePercent);
    }

    [Fact]
    public void Switching_profile_reloads_its_display_preference()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings
            {
                Language = "pt-BR",
                ActiveProfileId = "sala",
                EnvironmentProfiles =
                [
                    new EnvironmentProfile
                    {
                        Id = "sala", Name = "Sala",
                        TvWidth = 1920, TvHeight = 1080, TvRefreshHz = 60, TvScalePercent = 100,
                    },
                    new EnvironmentProfile
                    {
                        Id = "quarto", Name = "Quarto",
                        TvWidth = 3840, TvHeight = 2160, TvRefreshHz = 30, TvScalePercent = 150,
                    },
                ],
            },
        };
        LocalizationService.Instance.Initialize(store);

        var vm = CreateViewModel(store, DiscoveredModes);

        Assert.Equal(new DisplayPreference(1920, 1080, 60, 100), vm.CurrentDisplayPreference);

        vm.SelectedEnvironmentProfile = Assert.Single(vm.EnvironmentProfiles,
            profile => profile.Id == "quarto");
        Assert.Equal(new DisplayPreference(3840, 2160, 30, 150), vm.CurrentDisplayPreference);

        vm.SelectedEnvironmentProfile = Assert.Single(vm.EnvironmentProfiles,
            profile => profile.Id == "sala");
        Assert.Equal(new DisplayPreference(1920, 1080, 60, 100), vm.CurrentDisplayPreference);

        Assert.Equal("sala", store.Current.ActiveProfileId);
        Assert.Equal(1920, store.Current.TvWidth);
        Assert.Equal(60, store.Current.TvRefreshHz);
    }

    [Fact]
    public void Missing_mode_enumeration_preserves_the_saved_preference()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings
            {
                Language = "pt-BR",
                ActiveProfileId = "sala",
                EnvironmentProfiles =
                [
                    new EnvironmentProfile
                    {
                        Id = "sala", Name = "Sala",
                        TvWidth = 1920, TvHeight = 1080, TvRefreshHz = 60, TvScalePercent = 125,
                    },
                ],
            },
        };
        LocalizationService.Instance.Initialize(store);

        var vm = CreateViewModel(store, []);

        Assert.NotNull(vm.TvModesStatus);
        Assert.Equal(new DisplayPreference(1920, 1080, 60, 125), vm.CurrentDisplayPreference);

        vm.MinimizeToTray = !vm.MinimizeToTray;

        Assert.Equal(1920, store.Current.TvWidth);
        Assert.Equal(1080, store.Current.TvHeight);
        Assert.Equal(60, store.Current.TvRefreshHz);
        Assert.Equal(125, store.Current.TvScalePercent);
        var profile = Assert.Single(store.Current.EnvironmentProfiles!);
        Assert.Equal(1920, profile.TvWidth);
        Assert.Equal(1080, profile.TvHeight);
        Assert.Equal(60, profile.TvRefreshHz);
        Assert.Equal(125, profile.TvScalePercent);
    }

    [Fact]
    public void Environment_profile_option_roundtrip_preserves_the_display_preference()
    {
        var model = new EnvironmentProfile
        {
            Id = "sala",
            Name = "Sala",
            TvWidth = 2560,
            TvHeight = 1440,
            TvRefreshHz = 144,
            TvScalePercent = 150,
        };

        var roundtrip = EnvironmentProfileOption.FromModel(model).ToModel();

        Assert.Equal(model, roundtrip);
        Assert.Equal(new DisplayPreference(2560, 1440, 144, 150),
            new DisplayPreference(roundtrip.TvWidth, roundtrip.TvHeight,
                roundtrip.TvRefreshHz, roundtrip.TvScalePercent));
    }

    private static MainWindowViewModel CreateViewModel(
        FakeSettingsStore store,
        IReadOnlyList<DisplayModeOption> modes) =>
        new(LocalizationService.Instance, store, new FakeJournal(),
            discoverDisplayModes: () => modes);

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public AppSettings Current { get; set; } = new();

        public AppSettings Load() => Current;

        public void Save(AppSettings settings) => Current = settings;
    }

    private sealed class FakeJournal : ISessionJournalStore
    {
        public SessionJournalEntry? Load() => null;

        public SessionJournalInspection Inspect() => new(SessionJournalStatus.Missing, null);

        public void Save(SessionJournalEntry entry)
        {
        }

        public void Clear()
        {
        }
    }
}

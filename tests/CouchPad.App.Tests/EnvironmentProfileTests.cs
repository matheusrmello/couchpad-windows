using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.Core.Automation;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

[Collection("Localization")]
public sealed class EnvironmentProfileTests
{
    private static readonly DisplayTargetOption TvSala =
        new("tv-sala", 11596, 29031, "SAMSUNG") { IsAvailable = true, IsHdmi = true };
    private static readonly DisplayTargetOption TvQuarto =
        new("tv-quarto", 7789, 301, "LG TV") { IsAvailable = true, IsHdmi = true };
    private static readonly DisplayTargetOption Monitor =
        new("monitor", 58117, 9986, "AOC") { IsAvailable = true, IsActive = true };
    private static readonly AudioEndpointOption AudioSala =
        new("audio-sala", "SAMSUNG", IsAvailable: true, IsDisplayAudio: true);
    private static readonly AudioEndpointOption AudioQuarto =
        new("audio-quarto", "LG TV", IsAvailable: true, IsDisplayAudio: true);
    private static readonly AudioEndpointOption AudioMonitor =
        new("audio-monitor", "AOC", IsAvailable: true, IsCurrent: true);

    [Fact]
    public void Legacy_settings_migrate_to_one_default_profile()
    {
        var store = new FakeSettingsStore { Current = SettingsFor("tv-sala", "audio-sala") };
        LocalizationService.Instance.Initialize(store);

        var vm = CreateViewModel(store);

        Assert.Single(vm.EnvironmentProfiles);
        Assert.Same(vm.EnvironmentProfiles[0], vm.SelectedEnvironmentProfile);
        Assert.Equal(TvSala, vm.SelectedTvTarget);
        Assert.Equal(Monitor, vm.SelectedInternalTarget);
        Assert.Equal(AudioSala, vm.SelectedTvAudioEndpoint);
    }

    [Fact]
    public void Switching_profile_loads_its_devices_and_persists_active_id()
    {
        var store = new FakeSettingsStore { Current = TwoProfileSettings() };
        LocalizationService.Instance.Initialize(store);

        var vm = CreateViewModel(store);

        Assert.Equal(TvSala, vm.SelectedTvTarget);
        Assert.Equal(AudioSala, vm.SelectedTvAudioEndpoint);

        vm.SelectedEnvironmentProfile = vm.EnvironmentProfiles.Single(profile => profile.Id == "quarto");

        Assert.Equal(TvQuarto, vm.SelectedTvTarget);
        Assert.Equal(AudioQuarto, vm.SelectedTvAudioEndpoint);
        Assert.Equal("quarto", store.Current.ActiveProfileId);
        Assert.Equal("tv-quarto", store.Current.TvDevicePath);
        Assert.Equal("audio-quarto", store.Current.TvAudioEndpointId);
        Assert.Equal(2, store.Current.EnvironmentProfiles!.Count);
    }

    [Fact]
    public void Changing_a_selector_updates_the_active_profile()
    {
        var store = new FakeSettingsStore { Current = TwoProfileSettings() };
        LocalizationService.Instance.Initialize(store);

        var vm = CreateViewModel(store);
        var other = new DisplayTargetOption("tv-office", 42, 43, "Office TV") { IsAvailable = true, IsHdmi = true };
        vm.SelectedTvTarget = other;

        Assert.Equal("tv-office", store.Current.TvDevicePath);
        var sala = Assert.Single(store.Current.EnvironmentProfiles!, profile => profile.Id == "sala");
        Assert.Equal("tv-office", sala.TvDevicePath);
        var quarto = Assert.Single(store.Current.EnvironmentProfiles!, profile => profile.Id == "quarto");
        Assert.Equal("tv-quarto", quarto.TvDevicePath);
    }

    [Fact]
    public void New_profile_copies_selection_and_delete_keeps_one_profile()
    {
        var store = new FakeSettingsStore { Current = SettingsFor("tv-sala", "audio-sala") };
        LocalizationService.Instance.Initialize(store);
        var vm = CreateViewModel(store);

        vm.NewEnvironmentProfileCommand.Execute(null);

        Assert.Equal(2, vm.EnvironmentProfiles.Count);
        Assert.Equal(TvSala, vm.SelectedTvTarget);
        Assert.Equal(AudioSala, vm.SelectedTvAudioEndpoint);
        Assert.True(vm.CanDeleteEnvironmentProfile);

        vm.SelectedProfileName = "Quarto";
        Assert.Equal("Quarto", vm.SelectedEnvironmentProfile!.Name);
        Assert.Contains(store.Current.EnvironmentProfiles!, profile => profile.Name == "Quarto");

        vm.DeleteEnvironmentProfileCommand.Execute(null);

        Assert.Single(vm.EnvironmentProfiles);
        Assert.False(vm.CanDeleteEnvironmentProfile);
        Assert.Single(store.Current.EnvironmentProfiles!);
    }

    private static MainWindowViewModel CreateViewModel(FakeSettingsStore store) =>
        new(LocalizationService.Instance, store, new FakeJournal(),
            productionMode: true, configuredHardware: store.Current,
            discoverTargets: () => [TvSala, TvQuarto, Monitor],
            discoverAudioEndpoints: () => [AudioSala, AudioQuarto, AudioMonitor]);

    private static AppSettings SettingsFor(string tvPath, string audioId) => new()
    {
        TvDevicePath = tvPath,
        TvEdidManufacturer = 11596,
        TvEdidProduct = 29031,
        TvExactName = "SAMSUNG",
        InternalDevicePath = "monitor",
        InternalEdidManufacturer = 58117,
        InternalEdidProduct = 9986,
        InternalExactName = "AOC",
        TvAudioEndpointId = audioId,
    };

    private static AppSettings TwoProfileSettings() => new()
    {
        ActiveProfileId = "sala",
        EnvironmentProfiles =
        [
            new EnvironmentProfile
            {
                Id = "sala",
                Name = "Sala",
                TvDevicePath = "tv-sala",
                TvEdidManufacturer = 11596,
                TvEdidProduct = 29031,
                TvExactName = "SAMSUNG",
                InternalDevicePath = "monitor",
                InternalEdidManufacturer = 58117,
                InternalEdidProduct = 9986,
                InternalExactName = "AOC",
                TvAudioEndpointId = "audio-sala",
            },
            new EnvironmentProfile
            {
                Id = "quarto",
                Name = "Quarto",
                TvDevicePath = "tv-quarto",
                TvEdidManufacturer = 7789,
                TvEdidProduct = 301,
                TvExactName = "LG TV",
                InternalDevicePath = "monitor",
                InternalEdidManufacturer = 58117,
                InternalEdidProduct = 9986,
                InternalExactName = "AOC",
                TvAudioEndpointId = "audio-quarto",
            },
        ],
        TvDevicePath = "tv-sala",
        TvEdidManufacturer = 11596,
        TvEdidProduct = 29031,
        TvExactName = "SAMSUNG",
        InternalDevicePath = "monitor",
        InternalEdidManufacturer = 58117,
        InternalEdidProduct = 9986,
        InternalExactName = "AOC",
        TvAudioEndpointId = "audio-sala",
    };

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

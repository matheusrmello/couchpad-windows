using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

public sealed class MainWindowViewModelDisplayTests
{
    [Fact]
    public void Startup_setting_reads_real_state_and_writes_only_when_changed()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { StartWithWindows = true } };
        LocalizationService.Instance.Initialize(store);
        var writes = 0;
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            readStartup: () => false, configureStartup: enabled => { Assert.True(enabled); writes++; });
        Assert.False(vm.StartWithWindows);
        Assert.Equal(0, writes);
        vm.StartWithWindows = true;
        Assert.Equal(1, writes);
        Assert.True(store.Current.StartWithWindows);
        Assert.False(vm.StartupError);
    }

    [Fact]
    public void Startup_write_failure_reverts_toggle_and_reports_error()
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            readStartup: () => false, configureStartup: _ => throw new UnauthorizedAccessException());
        vm.StartWithWindows = true;
        Assert.False(vm.StartWithWindows);
        Assert.True(vm.StartupError);
    }

    [Fact]
    public void Unavailable_startup_is_disabled_and_preserves_saved_preference()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { StartWithWindows = true } };
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal());
        Assert.False(vm.CanConfigureStartup);
        Assert.False(vm.StartWithWindows);
        vm.LauncherIndex = 1;
        Assert.True(store.Current.StartWithWindows);
    }

    [Fact]
    public void Launcher_choice_is_persisted_without_losing_the_legacy_custom_path()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { CustomLauncherPath = @"C:\Legacy" } };
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal());
        vm.LauncherIndex = 1;
        Assert.Equal(1, store.Current.LauncherIndex);
        Assert.Equal(@"C:\Legacy", store.Current.CustomLauncherPath);
    }

    [Fact]
    public void Countdown_delays_are_saved_independently()
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            displayService: new SimulatedDisplayService(TimeSpan.Zero),
            audioService: new SimulatedAudioService(TimeSpan.Zero), discoverTargets: Discover);
        vm.ActivationDelayIndex = 1;
        vm.DeactivationDelayIndex = 4;
        Assert.Equal(1, store.Current.ActivationDelayIndex);
        Assert.Equal(4, store.Current.DeactivationDelayIndex);
    }

    [Fact]
    public void Legacy_mirror_setting_disables_automation_and_uses_default_mode()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { DisplayModeIndex = 2, AutomationEnabled = true } };
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            displayService: new SimulatedDisplayService(TimeSpan.Zero),
            audioService: new SimulatedAudioService(TimeSpan.Zero), discoverTargets: Discover);
        Assert.Equal(0, vm.DisplayModeIndex);
        Assert.False(vm.AutomationEnabled);
    }

    [Fact]
    public void Display_selection_and_write_opt_in_are_persisted()
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(
            LocalizationService.Instance,
            store,
            new FakeJournal(),
            displayService: new SimulatedDisplayService(TimeSpan.Zero),
            audioService: new SimulatedAudioService(TimeSpan.Zero),
            discoverTargets: Discover);

        var tv = Assert.Single(vm.DisplayTargets, target => target.FriendlyName == "SAMSUNG");
        var monitor = Assert.Single(vm.DisplayTargets, target => target.FriendlyName == "27G2G4");

        vm.SelectedTvTarget = tv;
        vm.SelectedInternalTarget = monitor;
        vm.AllowDisplayWrites = true;

        Assert.Equal("tv-path", store.Current.TvDevicePath);
        Assert.Equal((ushort)11596, store.Current.TvEdidManufacturer);
        Assert.Equal((ushort)29031, store.Current.TvEdidProduct);
        Assert.Equal("SAMSUNG", store.Current.TvExactName);
        Assert.Equal("monitor-path", store.Current.InternalDevicePath);
        Assert.Equal((ushort)58117, store.Current.InternalEdidManufacturer);
        Assert.Equal((ushort)9986, store.Current.InternalEdidProduct);
        Assert.Equal("27G2G4", store.Current.InternalExactName);
        Assert.True(store.Current.AllowDisplayWrites);
    }

    [Fact]
    public void Audio_selection_persists_and_disabling_revokes_writes()
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var revoked = false;
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            displayService: new SimulatedDisplayService(TimeSpan.Zero), audioService: new SimulatedAudioService(TimeSpan.Zero),
            discoverAudioEndpoints: () => [new AudioEndpointOption("tv-id", "TV")], revokeAudioWrites: () => revoked = true);
        vm.SelectedTvAudioEndpoint = Assert.Single(vm.AudioEndpoints);
        vm.AllowAudioWrites = true;
        Assert.Equal("tv-id", store.Current.TvAudioEndpointId);
        Assert.True(store.Current.AllowAudioWrites);
        vm.AllowAudioWrites = false;
        Assert.True(revoked);
        vm.AllowAudioWrites = true;
        vm.AutomationEnabled = true;
        Assert.False(vm.AutomationEnabled);
        vm.ResetCommand.Execute(null);
        Assert.False(store.Current.AllowAudioWrites);
        Assert.Null(store.Current.TvAudioEndpointId);
    }

    [Fact]
    public void Reset_clears_display_selection_and_write_opt_in()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings
            {
                AllowDisplayWrites = true,
                TvDevicePath = "tv-path",
                TvEdidManufacturer = 11596,
                TvEdidProduct = 29031,
                InternalDevicePath = "monitor-path",
                InternalEdidManufacturer = 58117,
                InternalEdidProduct = 9986,
            },
        };
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(
            LocalizationService.Instance,
            store,
            new FakeJournal(),
            displayService: new SimulatedDisplayService(TimeSpan.Zero),
            audioService: new SimulatedAudioService(TimeSpan.Zero),
            discoverTargets: Discover);

        vm.ResetCommand.Execute(null);

        Assert.False(store.Current.AllowDisplayWrites);
        Assert.Null(store.Current.TvDevicePath);
        Assert.Null(store.Current.InternalDevicePath);
    }

    [Fact]
    public void Disabling_writes_revokes_the_session_and_cannot_reenable_automation()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { AllowDisplayWrites = true } };
        LocalizationService.Instance.Initialize(store);
        var display = new RevocableDisplay();
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            displayService: display, audioService: new SimulatedAudioService(TimeSpan.Zero), discoverTargets: Discover);
        vm.AllowDisplayWrites = false;
        Assert.False(display.CanApply);
        vm.AllowDisplayWrites = true;
        vm.AutomationEnabled = true;
        Assert.False(vm.AutomationEnabled);
    }

    private sealed class RevocableDisplay : IDisplayService, IDisplayWriteControl
    {
        public bool CanApply { get; private set; } = true;
        public void DisableNewWrites() => CanApply = false;
        public DisplayState GetCurrent() => new(DisplayMode.InternalOnly, true, false);
        public Task<DisplayState> ApplyAsync(DisplayMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestoreAsync(DisplayState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static IReadOnlyList<DisplayTargetOption> Discover() =>
        new[]
        {
            new DisplayTargetOption("tv-path", 11596, 29031, "SAMSUNG"),
            new DisplayTargetOption("monitor-path", 58117, 9986, "27G2G4"),
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

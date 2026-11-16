using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

[Collection("Localization")]
public sealed class MainWindowViewModelDisplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Guided_setup_requires_confirmation_and_refuses_ambiguous_audio(bool duplicate)
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var tv = new DisplayTargetOption("tv", 1, 2, "SAMSUNG") { IsAvailable = true, IsHdmi = true };
        var monitor = new DisplayTargetOption("monitor", 3, 4, "AOC") { IsAvailable = true, IsActive = true };
        var audio = new AudioEndpointOption("tv-audio", "SAMSUNG (HD Audio)", IsAvailable: true, IsDisplayAudio: true);
        var defaultAudio = new AudioEndpointOption("monitor-audio", "AOC", IsAvailable: true, IsCurrent: true);
        var restarts = 0;
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            productionMode: true, canStartRecovery: true, discoverTargets: () => [tv, monitor],
            discoverAudioEndpoints: () => duplicate
                ? [audio with { Id = "other", Name = "SAMSUNG -2", IsAvailable = false, CanAppearAfterDisplayChange = true }, audio, defaultAudio]
                : [audio, defaultAudio], restartForHardware: () => restarts++);

        vm.SuggestHardwareCommand.Execute(null);

        Assert.Equal(tv, vm.SelectedTvTarget);
        Assert.Equal(monitor, vm.SelectedInternalTarget);
        Assert.Equal(defaultAudio, vm.AudioEndpoints[0]);
        Assert.False(vm.AutomationEnabled);
        Assert.Equal(0, restarts);
        if (duplicate)
        {
            Assert.Null(vm.SelectedTvAudioEndpoint);
            Assert.False(vm.ApplyHardwareCommand.CanExecute(null));
            vm.SelectedTvAudioEndpoint = audio;
        }
        Assert.Equal(audio, vm.SelectedTvAudioEndpoint);
        Assert.True(vm.ApplyHardwareCommand.CanExecute(null));
        await vm.ApplyHardwareCommand.ExecuteAsync(null);
        Assert.Equal(1, restarts);
        Assert.False(store.Current.AutomationEnabled);
        Assert.False(store.Current.AllowDisplayWrites);
        Assert.Equal(audio.Id, store.Current.TvAudioEndpointId);
    }

    [Fact]
    public async Task Audio_test_routes_by_selected_id_and_refuses_unavailable_output()
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var selected = new AudioEndpointOption("test-device", "TV", IsAvailable: true);
        var inactive = new AudioEndpointOption("inactive", "TV", CanAppearAfterDisplayChange: true);
        var defaultAudio = new AudioEndpointOption("default", "AOC", IsAvailable: true, IsCurrent: true);
        var played = new List<string>();
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            discoverAudioEndpoints: () => [inactive, selected, defaultAudio], playAudioTest: played.Add);
        Assert.Equal(new[] { defaultAudio, selected, inactive }, vm.AudioEndpoints.ToArray());
        vm.SelectedTvAudioEndpoint = inactive;
        Assert.False(vm.TestAudioCommand.CanExecute(null));
        vm.SelectedTvAudioEndpoint = selected;
        Assert.True(vm.TestAudioCommand.CanExecute(null));
        await vm.TestAudioCommand.ExecuteAsync(null);
        Assert.Equal(selected.Id, Assert.Single(played));
    }

    [Fact]
    public void Production_automation_requires_the_currently_loaded_device_selection()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings
            {
                TvDevicePath = "tv-path", TvEdidManufacturer = 11596, TvEdidProduct = 29031,
                InternalDevicePath = "monitor-path", InternalEdidManufacturer = 58117, InternalEdidProduct = 9986,
                TvAudioEndpointId = "tv-audio",
            }
        };
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            productionMode: true, configuredHardware: store.Current, discoverTargets: Discover);
        Assert.True(vm.CanToggleAutomation);
        Assert.False(vm.AutomationEnabled);
        vm.AutomationEnabled = true;
        Assert.True(store.Current.AllowDisplayWrites);
        Assert.True(store.Current.AllowAudioWrites);
        vm.SelectedTvAudioEndpoint = new AudioEndpointOption("different-audio", "Other");
        Assert.False(vm.CanToggleAutomation);
        Assert.False(vm.AutomationEnabled);
        Assert.False(store.Current.AllowDisplayWrites);
        Assert.Equal("different-audio", store.Current.TvAudioEndpointId);
    }

    [Fact]
    public void Production_without_configured_hardware_cannot_enable_simulated_automation()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { AutomationEnabled = true } };
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(), productionMode: true);
        Assert.False(vm.CanToggleAutomation);
        Assert.True(vm.ShowHardwareSetup);
        vm.AutomationEnabled = true;
        Assert.False(vm.AutomationEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Assisted_recovery_requires_a_blocked_session_and_available_rescue(bool available)
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new UnreadableJournal(),
            canStartRecovery: available, recoveryFailed: true);
        var requests = 0;
        vm.RecoveryRequested += (_, _) => requests++;
        Assert.Equal(available, vm.CanRetryRecovery);
        Assert.True(vm.RecoveryFailed);
        vm.RetryRecoveryCommand.Execute(null);
        Assert.Equal(available ? 1 : 0, requests);
    }

    [Fact]
    public void Reopening_after_recovery_starts_with_automation_off()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { AutomationEnabled = true } };
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            suspendAutomation: true, canStartRecovery: true);
        Assert.False(vm.AutomationEnabled);
        Assert.False(vm.CanRetryRecovery);
        Assert.True(store.Current.AutomationEnabled);
    }

    [Fact]
    public async Task Exit_from_idle_is_allowed_without_changing_saved_automation_preference()
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal());
        Assert.True(await vm.PrepareExitAsync());
        Assert.False(store.Current.AutomationEnabled);
    }

    [Fact]
    public async Task Exit_is_refused_when_recovery_is_uncertain()
    {
        var store = new FakeSettingsStore();
        LocalizationService.Instance.Initialize(store);
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new UnreadableJournal());
        Assert.False(await vm.PrepareExitAsync());
    }
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

    private sealed class UnreadableJournal : ISessionJournalStore
    {
        public SessionJournalEntry? Load() => null;
        public SessionJournalInspection Inspect() => new(SessionJournalStatus.Unreadable, null);
        public void Save(SessionJournalEntry entry) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}

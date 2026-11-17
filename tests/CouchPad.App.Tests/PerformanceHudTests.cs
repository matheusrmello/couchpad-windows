using CouchPad.App.Localization;
using CouchPad.App.Performance;
using CouchPad.App.ViewModels;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Performance;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

[Collection("Localization")]
public sealed class PerformanceHudTests
{
    private static readonly GamepadButtons Chord = GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.Y;
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void View_model_formats_values_and_hides_unselected_metrics()
    {
        var viewModel = new PerformanceHudViewModel();
        var settings = PerformanceHudSettings.Default with
        {
            Metrics = [PerformanceMetric.Cpu, PerformanceMetric.Gpu, PerformanceMetric.Ram, PerformanceMetric.Process],
        };
        viewModel.ApplySettings(settings);
        viewModel.Update(new PerformanceSnapshot(
            32, 12, 91, 12UL * 1024 * 1024 * 1024, 32UL * 1024 * 1024 * 1024,
            7UL * 1024 * 1024 * 1024, 12UL * 1024 * 1024 * 1024,
            "Game", 1920, 1080, 120, Start));

        Assert.Equal("32%", viewModel.CpuText);
        Assert.Equal("91%", viewModel.GpuText);
        Assert.Contains("GB", viewModel.RamText);
        Assert.Equal("Game", viewModel.ProcessText);
        Assert.True(viewModel.ShowCpu);
        Assert.False(viewModel.ShowVram);
        Assert.False(viewModel.ShowEmpty);
        Assert.True(viewModel.CpuBarWidth > 0);
    }

    [Fact]
    public void View_model_uses_dash_for_missing_values_and_empty_state()
    {
        var viewModel = new PerformanceHudViewModel();
        viewModel.ApplySettings(PerformanceHudSettings.Default with { Metrics = [] });
        viewModel.Update(PerformanceSnapshot.Empty);

        Assert.True(viewModel.ShowEmpty);
        Assert.Equal("—", viewModel.CpuText);
        Assert.Equal("—", viewModel.GpuText);
        Assert.Equal(0, viewModel.CpuBarWidth);
    }

    [Fact]
    public void View_model_formats_fps_and_falls_back_to_dash()
    {
        var viewModel = new PerformanceHudViewModel();
        viewModel.ApplySettings(PerformanceHudSettings.Default with
        {
            Metrics = [PerformanceMetric.FramesPerSecond],
        });
        viewModel.Update(new PerformanceSnapshot(
            null, null, null, null, null, null, null, null, null, null, null, Start, 144));

        Assert.True(viewModel.ShowFps);
        Assert.Equal("144 FPS", viewModel.FpsText);

        viewModel.Update(PerformanceSnapshot.Empty);

        Assert.Equal("—", viewModel.FpsText);
    }

    [Fact]
    public void Coordinator_shows_only_in_session_and_toggles_visibility()
    {
        var settings = PerformanceHudSettings.Default with
        {
            Enabled = true,
            ShowOnlyInSession = true,
        };
        var presenter = new FakePresenter();
        using var coordinator = new PerformanceHudCoordinator(
            new FakeSampler(), new FakeButtons(), presenter, settings, new FakeTimeProvider());

        coordinator.Start();
        Assert.False(presenter.IsVisible);

        coordinator.SetSessionActive(true);
        Assert.True(presenter.IsVisible);
        Assert.Equal(1, presenter.ShowCount);

        coordinator.Toggle();
        Assert.False(presenter.IsVisible);
        coordinator.Toggle();
        Assert.True(presenter.IsVisible);

        coordinator.SetSessionActive(false);
        Assert.False(presenter.IsVisible);
    }

    [Fact]
    public void Coordinator_toggles_once_when_chord_is_held()
    {
        var settings = PerformanceHudSettings.Default with
        {
            Enabled = true,
            ShowOnlyInSession = false,
        };
        var presenter = new FakePresenter();
        var buttons = new FakeButtons();
        var time = new FakeTimeProvider { UtcNow = Start };
        using var coordinator = new PerformanceHudCoordinator(
            new FakeSampler(), buttons, presenter, settings, time);

        coordinator.Start();
        Assert.True(presenter.IsVisible);

        buttons.Raise(Chord);
        time.UtcNow = Start.AddMilliseconds(100);
        buttons.Raise(Chord);
        time.UtcNow = Start.AddMilliseconds(900);
        buttons.Raise(Chord);

        Assert.False(presenter.IsVisible);
        Assert.Equal(1, presenter.HideCount);

        buttons.Raise(GamepadButtons.None);
        time.UtcNow = Start.AddMilliseconds(2000);
        buttons.Raise(Chord);
        time.UtcNow = Start.AddMilliseconds(2900);
        buttons.Raise(Chord);
        Assert.True(presenter.IsVisible);
    }

    [Fact]
    public void Settings_view_model_roundtrips_selection()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { Language = "pt-BR" } };
        LocalizationService.Instance.Initialize(store);
        var expected = new PerformanceHudSettings(
            true,
            [PerformanceMetric.Cpu, PerformanceMetric.Vram, PerformanceMetric.Display, PerformanceMetric.FramesPerSecond],
            PerformanceHudPosition.BottomLeft,
            0.55,
            1.25,
            false,
            "LB+RB+Back");

        var viewModel = new PerformanceHudSettingsViewModel(expected);
        var actual = viewModel.ToSettings();

        Assert.True(viewModel.CpuSelected);
        Assert.False(viewModel.GpuSelected);
        Assert.True(viewModel.VramSelected);
        Assert.True(viewModel.DisplaySelected);
        Assert.True(viewModel.FpsSelected);
        Assert.Equal("LB+RB+Back", viewModel.SelectedChord?.Key);
        Assert.Equal("bottomLeft", viewModel.SelectedPosition?.Key);
        Assert.Equal(expected.Metrics, actual.Metrics);
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Opacity, actual.Opacity);
        Assert.Equal(expected.Scale, actual.Scale);
        Assert.Equal(expected.ShowOnlyInSession, actual.ShowOnlyInSession);
        Assert.Equal(expected.ChordKey, actual.ChordKey);
    }

    [Fact]
    public void Main_view_model_persists_hud_settings_and_notifies_controller()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings
            {
                Language = "pt-BR",
                PerformanceHudEnabled = true,
                PerformanceHudMetrics = ["cpu", "gpu"],
                PerformanceHudPosition = "bottomRight",
                PerformanceHudOpacity = 0.7,
                PerformanceHudScale = 1.1,
                PerformanceHudShowOnlyInSession = false,
                PerformanceHudToggleChord = "LB+RB+X",
            },
        };
        LocalizationService.Instance.Initialize(store);
        var hud = new FakeController();
        var vm = new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            performanceHud: hud);

        Assert.True(vm.PerformanceHudEnabled);
        Assert.True(vm.CanConfigurePerformanceHud);
        Assert.NotNull(hud.Applied);
        Assert.Equal(0.7, hud.Applied!.Opacity);
        Assert.Equal("Ativo", vm.HudStatusText);
        Assert.Equal("Inferior direito", vm.HudPositionText);
        Assert.Equal("LB + RB + X", vm.HudChordText);
        Assert.True(vm.HudShowsCpu);
        Assert.True(vm.HudShowsGpu);
        Assert.False(vm.HudShowsRam);
        Assert.True(vm.HudHasMetrics);
        Assert.False(vm.PerformanceHudShowOnlyInSession);

        vm.PerformanceHudEnabled = false;
        Assert.False(store.Current.PerformanceHudEnabled);
        Assert.False(hud.Applied!.Enabled);

        var changed = new PerformanceHudSettings(
            true, [PerformanceMetric.Ram, PerformanceMetric.FramesPerSecond], PerformanceHudPosition.TopLeft, 0.6, 1.2, true, "LB+RB+Back");
        vm.ApplyHudSettings(changed);

        Assert.True(store.Current.PerformanceHudEnabled);
        Assert.Equal(["ram", "fps"], store.Current.PerformanceHudMetrics);
        Assert.Equal("topLeft", store.Current.PerformanceHudPosition);
        Assert.Equal("LB+RB+Back", store.Current.PerformanceHudToggleChord);
        Assert.Equal(changed.Enabled, hud.Applied!.Enabled);
        Assert.Equal(changed.Metrics, hud.Applied.Metrics);
        Assert.Equal(changed.Position, hud.Applied.Position);
        Assert.Equal(changed.Opacity, hud.Applied.Opacity);
        Assert.Equal(changed.Scale, hud.Applied.Scale);
        Assert.Equal(changed.ShowOnlyInSession, hud.Applied.ShowOnlyInSession);
        Assert.Equal(changed.ChordKey, hud.Applied.ChordKey);
        Assert.True(vm.HudShowsRam);
        Assert.True(vm.HudShowsFps);
        Assert.False(vm.HudShowsCpu);
        Assert.Equal("Superior esquerdo", vm.HudPositionText);
        Assert.Equal("LB + RB + Back", vm.HudChordText);

        vm.PerformanceHudShowOnlyInSession = false;
        Assert.False(store.Current.PerformanceHudShowOnlyInSession);
        Assert.False(hud.Applied!.ShowOnlyInSession);
    }

    private sealed class FakeSampler : IPerformanceSampler
    {
        public Task<PerformanceSnapshot> SampleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PerformanceSnapshot.Empty);
    }

    private sealed class FakeButtons : IGamepadButtonSource
    {
        public event EventHandler<GamepadButtonsChangedEventArgs>? ButtonsReported;
        public void Raise(GamepadButtons buttons) => ButtonsReported?.Invoke(this, new GamepadButtonsChangedEventArgs(buttons));
        public void Dispose() { }
    }

    private sealed class FakePresenter : IPerformanceHudPresenter
    {
        public bool IsVisible { get; private set; }
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public void ApplySettings(PerformanceHudSettings settings) { }
        public void Show(PerformanceSnapshot snapshot, PerformanceHudSettings settings) { IsVisible = true; ShowCount++; }
        public void Update(PerformanceSnapshot snapshot, PerformanceHudSettings settings) { }
        public void Hide() { IsVisible = false; HideCount++; }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class FakeController : IPerformanceHudController
    {
        public bool Enabled => Applied?.Enabled == true;
        public bool IsVisible => false;
        public PerformanceHudSettings? Applied { get; private set; }
        public event EventHandler? Changed { add { } remove { } }
        public void ApplySettings(PerformanceHudSettings settings) => Applied = settings;
        public void SetEnabled(bool enabled) => Applied = (Applied ?? PerformanceHudSettings.Default) with { Enabled = enabled };
        public void Toggle() { }
        public void SetSessionActive(bool active) { }
    }

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
        public void Save(SessionJournalEntry entry) { }
        public void Clear() { }
    }
}

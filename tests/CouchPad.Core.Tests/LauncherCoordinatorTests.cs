using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;
using CouchPad.Core.Launchers;

namespace CouchPad.Core.Tests;

public sealed class LauncherCoordinatorTests
{
    [Fact]
    public async Task Launcher_starts_after_active_journal_and_closes_after_restore()
    {
        var display = new SimulatedDisplayService();
        var audio = new SimulatedAudioService();
        var journal = new Journal();
        var launcher = new FakeLauncher();
        launcher.OnStart = () => {
            Assert.Equal(SessionJournalEntry.ActiveState, journal.Entry?.State);
            Assert.Equal(DisplayMode.TvOnly, display.Current.Mode);
            Assert.Equal(AudioOutput.Hdmi, audio.Current);
        };
        launcher.OnClose = () => {
            Assert.Null(journal.Entry);
            Assert.Equal(DisplayMode.InternalOnly, display.Current.Mode);
            Assert.Equal(AudioOutput.Default, audio.Current);
        };
        var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal, launcher: launcher);
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, LauncherEnabled = true, Launcher = LauncherKind.Hydra });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        Assert.Equal(LauncherKind.Hydra, launcher.Selection?.Kind);
        await coordinator.GamepadDisconnectedAsync();
        Assert.Equal(1, launcher.Closed);
        Assert.Equal(AutomationState.WaitingForGamepad, coordinator.State);
    }

    [Fact]
    public async Task Launch_failure_is_reported_but_restore_still_works()
    {
        var display = new SimulatedDisplayService();
        var audio = new SimulatedAudioService();
        var journal = new Journal();
        var launcher = new FakeLauncher { FailStart = true };
        var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal, launcher: launcher);
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, LauncherEnabled = true });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        Assert.Equal(AutomationState.Active, coordinator.State);
        Assert.True(coordinator.LauncherFailed);
        await coordinator.GamepadDisconnectedAsync();
        Assert.Equal(DisplayMode.InternalOnly, display.Current.Mode);
        Assert.Equal(AudioOutput.Default, audio.Current);
        Assert.Null(journal.Entry);
        Assert.Equal(AutomationState.WaitingForGamepad, coordinator.State);
    }

    [Fact]
    public async Task Close_failure_does_not_prevent_display_audio_or_journal_restore()
    {
        var display = new SimulatedDisplayService();
        var audio = new SimulatedAudioService();
        var journal = new Journal();
        var launcher = new FakeLauncher { FailClose = true };
        var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal, launcher: launcher);
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, LauncherEnabled = true });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        await coordinator.GamepadDisconnectedAsync();
        Assert.True(coordinator.LauncherFailed);
        Assert.Equal(DisplayMode.InternalOnly, display.Current.Mode);
        Assert.Equal(AudioOutput.Default, audio.Current);
        Assert.Null(journal.Entry);
    }

    [Fact]
    public async Task Disabled_close_does_not_close_launcher()
    {
        var launcher = new FakeLauncher();
        var coordinator = new AutomationCoordinator(new SimulatedDisplayService(), new SimulatedAudioService(), new SystemDelayScheduler(), new Journal(), launcher: launcher);
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, LauncherEnabled = true, CloseLauncherOnActivate = false });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        await coordinator.GamepadDisconnectedAsync();
        Assert.Equal(0, launcher.Closed);
    }

    [Fact]
    public async Task Disconnect_cancels_hung_launcher_start_and_restores_display_and_audio()
    {
        var display = new SimulatedDisplayService();
        var audio = new SimulatedAudioService();
        var journal = new Journal();
        var launcher = new BlockingLauncher();
        var coordinator = new AutomationCoordinator(display, audio, new SystemDelayScheduler(), journal, launcher: launcher);
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, LauncherEnabled = true });
        var activation = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        await launcher.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.GamepadDisconnectedAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await activation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(DisplayMode.InternalOnly, display.Current.Mode);
        Assert.Equal(AudioOutput.Default, audio.Current);
        Assert.Null(journal.Entry);
        Assert.Equal(AutomationState.WaitingForGamepad, coordinator.State);
    }

    [Fact]
    public async Task Failed_display_apply_never_starts_launcher()
    {
        var launcher = new FakeLauncher();
        var coordinator = new AutomationCoordinator(new FailingDisplay(), new SimulatedAudioService(), new SystemDelayScheduler(), new Journal(), launcher: launcher);
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, LauncherEnabled = true });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Null(launcher.Selection);
    }

    private sealed class FailingDisplay : IDisplayService
    {
        public DisplayState GetCurrent() => new(DisplayMode.InternalOnly, true, false);
        public IReadOnlyList<DisplayModeOption> GetAvailableModes() => Array.Empty<DisplayModeOption>();
        public Task<DisplayState> ApplyAsync(DisplayMode mode, DisplayPreference? preference = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task RestoreAsync(DisplayState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class BlockingLauncher : ILauncherService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task StartAsync(LauncherSelection selection, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        public Task CloseOwnedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeLauncher : ILauncherService
    {
        public LauncherSelection? Selection;
        public int Closed;
        public bool FailStart, FailClose;
        public Action? OnStart, OnClose;
        public Task StartAsync(LauncherSelection selection, CancellationToken cancellationToken = default)
        {
            OnStart?.Invoke();
            if (FailStart) throw new InvalidOperationException();
            Selection = selection;
            return Task.CompletedTask;
        }
        public Task CloseOwnedAsync(CancellationToken cancellationToken = default)
        {
            OnClose?.Invoke();
            if (FailClose) throw new InvalidOperationException();
            Closed++;
            return Task.CompletedTask;
        }
    }

    private sealed class Journal : ISessionJournalStore
    {
        public SessionJournalEntry? Entry;
        public SessionJournalEntry? Load() => Entry;
        public SessionJournalInspection Inspect() => new(Entry is null ? SessionJournalStatus.Missing : SessionJournalStatus.Present, Entry);
        public void Save(SessionJournalEntry entry) => Entry = entry;
        public void Clear() => Entry = null;
    }
}

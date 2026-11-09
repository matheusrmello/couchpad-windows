using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;

namespace CouchPad.Core.Tests;

public sealed class AutomationCoordinatorTests
{
    [Fact]
    public async Task Disabled_gamepad_connect_stays_idle()
    {
        var (coordinator, display, _, _, _) = Create();

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Idle, coordinator.State);
        Assert.Empty(display.Applied);
    }

    [Fact]
    public async Task Enabling_without_gamepad_waits()
    {
        var (coordinator, _, _, _, _) = Create();

        await coordinator.SetEnabledAsync(true);

        Assert.Equal(AutomationState.WaitingForGamepad, coordinator.State);
    }

    [Fact]
    public async Task Disconnect_when_disabled_sets_idle()
    {
        var (coordinator, display, _, _, _) = Create();

        await coordinator.GamepadDisconnectedAsync();

        Assert.Equal(AutomationState.Idle, coordinator.State);
        Assert.Empty(display.Restored);
    }

    [Fact]
    public async Task Disconnect_without_active_snapshot_waits()
    {
        var (coordinator, display, _, _, _) = Create();
        await coordinator.SetEnabledAsync(true);

        await coordinator.GamepadDisconnectedAsync();

        Assert.Equal(AutomationState.WaitingForGamepad, coordinator.State);
        Assert.Null(coordinator.Snapshot);
        Assert.Empty(display.Restored);
    }

    [Fact]
    public async Task Activation_delay_goes_pending_then_active()
    {
        var (coordinator, display, audio, delay, journal) = Create();
        await coordinator.UpdateSettingsAsync(new AutomationSettings
        {
            Enabled = true,
            ActivationDelay = TimeSpan.FromSeconds(2),
            DisplayMode = DisplayMode.TvOnly,
            AudioMode = AudioMode.MoveToHdmi,
        });

        var activation = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Pending, coordinator.State);
        Assert.Equal(1, delay.PendingCount);

        delay.CompleteNext();
        await activation;

        Assert.Equal(AutomationState.Active, coordinator.State);
        Assert.Equal(new[] { DisplayMode.TvOnly }, display.Applied);
        Assert.Equal(AudioOutput.Hdmi, audio.Current);
        Assert.NotNull(journal.Entry);
        Assert.Equal(SessionJournalEntry.ActiveState, journal.Entry!.State);
        Assert.Equal(new DisplayState(DisplayMode.Extend, true, false), journal.Entry.RestoreDisplay);
        Assert.Equal(AudioOutput.Default, journal.Entry.RestoreAudio);
        Assert.Equal(1, journal.Saves);
    }

    [Fact]
    public async Task Disconnect_cancels_activation_delay_without_applying()
    {
        var (coordinator, display, _, delay, _) = Create();
        await coordinator.UpdateSettingsAsync(new AutomationSettings
        {
            Enabled = true,
            ActivationDelay = TimeSpan.FromSeconds(2),
        });

        var activation = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        Assert.Equal(AutomationState.Pending, coordinator.State);

        await coordinator.GamepadDisconnectedAsync();
        await activation;

        Assert.Equal(AutomationState.WaitingForGamepad, coordinator.State);
        Assert.Empty(display.Applied);
        Assert.Equal(0, delay.PendingCount);
    }

    [Fact]
    public async Task Disconnect_after_active_waits_deactivation_then_restores()
    {
        var (coordinator, display, audio, delay, journal) = Create();
        await coordinator.UpdateSettingsAsync(new AutomationSettings
        {
            Enabled = true,
            DeactivationDelay = TimeSpan.FromSeconds(1),
        });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        Assert.Equal(AutomationState.Active, coordinator.State);

        var disconnect = coordinator.GamepadDisconnectedAsync();

        Assert.Equal(AutomationState.Pending, coordinator.State);
        Assert.Equal(1, delay.PendingCount);

        delay.CompleteNext();
        await disconnect;

        Assert.Equal(AutomationState.WaitingForGamepad, coordinator.State);
        Assert.Single(display.Restored);
        Assert.Equal(AudioOutput.Default, audio.Current);
        Assert.Equal(1, journal.Clears);
        Assert.Null(journal.Entry);
    }

    [Fact]
    public async Task Reconnect_during_deactivation_cancels_restore()
    {
        var (coordinator, display, _, delay, _) = Create();
        await coordinator.UpdateSettingsAsync(new AutomationSettings
        {
            Enabled = true,
            DeactivationDelay = TimeSpan.FromSeconds(1),
        });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        var disconnect = coordinator.GamepadDisconnectedAsync();
        Assert.Equal(AutomationState.Pending, coordinator.State);

        var reconnect = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        await reconnect;
        await disconnect;

        Assert.Equal(AutomationState.Active, coordinator.State);
        Assert.Empty(display.Restored);
        Assert.Equal(0, delay.PendingCount);
    }

    [Fact]
    public async Task Apply_failure_sets_error_and_restores()
    {
        var (coordinator, display, _, _, journal) = Create();
        display.ApplyException = new InvalidOperationException("simulated failure");
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true });

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Contains("simulated failure", coordinator.LastError);
        Assert.Single(display.Restored);
        Assert.Null(journal.Entry);
    }

    [Fact]
    public async Task Disabling_while_active_restores_immediately()
    {
        var (coordinator, display, _, _, journal) = Create();
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        Assert.Equal(AutomationState.Active, coordinator.State);

        await coordinator.SetEnabledAsync(false);

        Assert.Equal(AutomationState.Idle, coordinator.State);
        Assert.Single(display.Restored);
        Assert.Equal(1, journal.Clears);
        Assert.Null(coordinator.Snapshot);
    }

    [Fact]
    public async Task Mode_change_while_active_reapplies()
    {
        var (coordinator, display, _, _, _) = Create();
        var settings = new AutomationSettings
        {
            Enabled = true,
            DisplayMode = DisplayMode.TvOnly,
        };
        await coordinator.UpdateSettingsAsync(settings);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        await coordinator.UpdateSettingsAsync(settings with { DisplayMode = DisplayMode.Extend });

        Assert.Equal(AutomationState.Active, coordinator.State);
        Assert.Equal(2, display.Applied.Count);
        Assert.Equal(DisplayMode.Extend, display.Applied[^1]);
        Assert.Equal(DisplayMode.Extend, coordinator.Snapshot!.DisplayMode);
    }

    [Fact]
    public async Task Failed_reapply_keeps_previous_snapshot_for_later_restore()
    {
        var (coordinator, display, _, _, journal) = Create();
        var settings = new AutomationSettings { Enabled = true };
        await coordinator.UpdateSettingsAsync(settings);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        Assert.NotNull(coordinator.Snapshot);

        display.ApplyException = new InvalidOperationException("reapply failed");
        await coordinator.UpdateSettingsAsync(settings with { DisplayMode = DisplayMode.Mirror });

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.NotNull(coordinator.Snapshot);
        Assert.NotNull(journal.Entry);

        await coordinator.SetEnabledAsync(false);

        Assert.Equal(AutomationState.Idle, coordinator.State);
        Assert.Null(coordinator.Snapshot);
        Assert.Null(journal.Entry);
    }

    [Fact]
    public async Task Reapply_keeps_the_original_restore_baseline()
    {
        var (coordinator, display, _, _, _) = Create();
        var baseline = new DisplayState(DisplayMode.Extend, PrimaryActive: true, TvActive: false);
        var settings = new AutomationSettings
        {
            Enabled = true,
            DisplayMode = DisplayMode.TvOnly,
        };
        await coordinator.UpdateSettingsAsync(settings);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        await coordinator.UpdateSettingsAsync(settings with { DisplayMode = DisplayMode.Mirror });
        await coordinator.SetEnabledAsync(false);

        Assert.Single(display.Restored);
        Assert.Equal(baseline, display.Restored[0]);
    }

    [Fact]
    public void Delay_options_map_indices_and_clamp()
    {
        Assert.Equal(TimeSpan.Zero, DelayOptions.FromIndex(0));
        Assert.Equal(TimeSpan.FromSeconds(1), DelayOptions.FromIndex(1));
        Assert.Equal(TimeSpan.FromSeconds(5), DelayOptions.FromIndex(4));
        Assert.Equal(TimeSpan.Zero, DelayOptions.FromIndex(-1));
        Assert.Equal(TimeSpan.Zero, DelayOptions.FromIndex(99));
    }

    [Fact]
    public async Task State_events_report_pending_applying_and_active()
    {
        var (coordinator, _, _, delay, _) = Create();
        var states = new List<AutomationState>();
        coordinator.StateChanged += (_, args) => states.Add(args.State);

        await coordinator.UpdateSettingsAsync(new AutomationSettings
        {
            Enabled = true,
            ActivationDelay = TimeSpan.FromSeconds(1),
        });

        var activation = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        delay.CompleteNext();
        await activation;

        Assert.Contains(AutomationState.Pending, states);
        Assert.Contains(AutomationState.Applying, states);
        Assert.Contains(AutomationState.Active, states);
    }

    private static (
        AutomationCoordinator Coordinator,
        FakeDisplayService Display,
        FakeAudioService Audio,
        ManualDelayScheduler Delay,
        FakeJournal Journal) Create()
    {
        var display = new FakeDisplayService();
        var audio = new FakeAudioService();
        var delay = new ManualDelayScheduler();
        var journal = new FakeJournal();
        var coordinator = new AutomationCoordinator(display, audio, delay, journal);

        return (coordinator, display, audio, delay, journal);
    }

    private sealed class FakeDisplayService : IDisplayService
    {
        public DisplayState Current { get; set; } =
            new(DisplayMode.Extend, PrimaryActive: true, TvActive: false);

        public List<DisplayMode> Applied { get; } = new();

        public List<DisplayState> Restored { get; } = new();

        public Exception? ApplyException { get; set; }

        public DisplayState GetCurrent() => Current;

        public Task<DisplayState> ApplyAsync(
            DisplayMode mode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ApplyException is not null)
            {
                throw ApplyException;
            }

            Applied.Add(mode);
            Current = mode switch
            {
                DisplayMode.TvOnly => new DisplayState(DisplayMode.TvOnly, false, true),
                DisplayMode.Mirror => new DisplayState(DisplayMode.Mirror, true, true),
                _ => new DisplayState(DisplayMode.Extend, true, true),
            };

            return Task.FromResult(Current);
        }

        public Task RestoreAsync(
            DisplayState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Restored.Add(state);
            Current = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudioService : IAudioService
    {
        public AudioOutput Current { get; set; } = AudioOutput.Default;

        public AudioOutput GetCurrent() => Current;

        public Task<AudioOutput> ApplyAsync(
            AudioMode mode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (mode == AudioMode.MoveToHdmi)
            {
                Current = AudioOutput.Hdmi;
            }

            return Task.FromResult(Current);
        }

        public Task RestoreAsync(
            AudioOutput state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = state;
            return Task.CompletedTask;
        }
    }

    private sealed class ManualDelayScheduler : IDelayScheduler
    {
        private readonly Queue<TaskCompletionSource<bool>> _pending = new();

        public int PendingCount => _pending.Count(tcs => !tcs.Task.IsCompleted);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay <= TimeSpan.Zero)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _pending.Enqueue(tcs);
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            return tcs.Task;
        }

        public void CompleteNext()
        {
            while (_pending.Count > 0)
            {
                var tcs = _pending.Dequeue();
                if (tcs.TrySetResult(true))
                {
                    return;
                }
            }

            throw new InvalidOperationException("Nenhum atraso pendente.");
        }
    }

    private sealed class FakeJournal : ISessionJournalStore
    {
        public SessionJournalEntry? Entry { get; private set; }

        public int Saves { get; private set; }

        public int Clears { get; private set; }

        public SessionJournalEntry? Load() => Entry;

        public void Save(SessionJournalEntry entry)
        {
            Entry = entry;
            Saves++;
        }

        public void Clear()
        {
            Entry = null;
            Clears++;
        }
    }
}

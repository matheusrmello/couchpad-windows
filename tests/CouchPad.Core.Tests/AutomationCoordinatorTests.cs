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
        Assert.Equal(2, journal.Saves);
        Assert.Equal(SessionJournalEntry.PendingState, journal.Saved[0].State);
        Assert.Equal(SessionJournalEntry.ActiveState, journal.Entry!.State);
        Assert.Equal(new DisplayState(DisplayMode.InternalOnly, true, false), journal.Entry.RestoreDisplay);
        Assert.Equal(AudioOutput.Default, journal.Entry.RestoreAudio);
        Assert.Equal(AudioOutput.Hdmi, journal.Entry.AppliedAudio);
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
    public async Task Journal_failure_before_apply_aborts_without_changing()
    {
        var (coordinator, display, _, _, journal) = Create();
        journal.FailOnSaveNumber = 1;
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true });

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Contains("journal", coordinator.LastError);
        Assert.Empty(display.Applied);
        Assert.Null(coordinator.Snapshot);
    }

    [Fact]
    public async Task Journal_failure_after_apply_keeps_pending_baseline()
    {
        var (coordinator, display, _, _, journal) = Create();
        journal.FailOnSaveNumber = 2;
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true });

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Active, coordinator.State);
        Assert.Single(display.Applied);
        Assert.Equal(1, journal.Saves);
        Assert.Equal(SessionJournalEntry.PendingState, journal.Entry!.State);
        Assert.Equal(new DisplayState(DisplayMode.InternalOnly, true, false), journal.Entry.RestoreDisplay);
    }

    [Fact]
    public async Task Journal_failure_before_reapply_keeps_existing_session_without_mutation()
    {
        var (coordinator, display, _, _, journal) = Create();
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true, DisplayMode = DisplayMode.TvOnly });
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        var original = coordinator.Snapshot;
        journal.FailOnSaveNumber = 3;

        await coordinator.UpdateSettingsAsync(coordinator.Settings with { DisplayMode = DisplayMode.Extend });

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Single(display.Applied);
        Assert.Equal(original, coordinator.Snapshot);
        Assert.Equal(SessionJournalEntry.ActiveState, journal.Entry?.State);
    }

    [Fact]
    public async Task Topology_snapshot_is_persisted_in_the_journal()
    {
        const string topology = "{\"schema\":1,\"paths\":[]}";
        var baseline = new DisplayState(DisplayMode.InternalOnly, PrimaryActive: true, TvActive: false)
        {
            Topology = topology,
        };
        var (coordinator, _, _, _, journal) = Create(baseline);
        await coordinator.UpdateSettingsAsync(new AutomationSettings { Enabled = true });

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(2, journal.Saves);
        Assert.Equal(topology, journal.Saved[0].RestoreDisplay!.Topology);
        Assert.Equal(topology, journal.Entry!.RestoreDisplay!.Topology);
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
        var baseline = new DisplayState(DisplayMode.InternalOnly, PrimaryActive: true, TvActive: false);
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
    public void Rescue_guard_requires_explicit_journal()
    {
        Assert.Throws<ArgumentException>(() => new AutomationCoordinator(
            new FakeDisplayService(), new FakeAudioService(), new ManualDelayScheduler(),
            rescueGuard: new FakeRescueGuard()));
    }

    [Fact]
    public async Task Guarded_activation_commits_after_success()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, _, _, _, _) = Create(rescueGuard: guard);
        await coordinator.SetEnabledAsync(true);

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        var lease = Assert.Single(guard.Leases);
        Assert.Equal("display apply", lease.Operation);
        Assert.Equal(1, lease.Commits);
        Assert.Equal(0, lease.Aborts);
        Assert.Equal(0, lease.Abandons);
        Assert.Equal(1, lease.Disposals);
    }

    [Fact]
    public async Task Guarded_activation_failure_aborts_after_restore()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, display, _, _, _) = Create(rescueGuard: guard);
        display.ApplyException = new InvalidOperationException("apply failed");
        await coordinator.SetEnabledAsync(true);

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        var lease = Assert.Single(guard.Leases);
        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Equal(0, lease.Commits);
        Assert.Equal(1, lease.Aborts);
        Assert.Equal(0, lease.Abandons);
        Assert.Single(display.Restored);
    }

    [Fact]
    public async Task Guarded_activation_abandons_when_rollback_fails()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, display, _, _, _) = Create(rescueGuard: guard);
        display.ApplyException = new InvalidOperationException("apply failed");
        display.RestoreException = new InvalidOperationException("restore failed");
        await coordinator.SetEnabledAsync(true);

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        var lease = Assert.Single(guard.Leases);
        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Equal(0, lease.Commits);
        Assert.Equal(0, lease.Aborts);
        Assert.Equal(1, lease.Abandons);
    }

    [Fact]
    public async Task Guarded_deactivation_commits_after_restore()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, _, _, _, _) = Create(rescueGuard: guard);
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        await coordinator.SetEnabledAsync(false);

        Assert.Equal(2, guard.Leases.Count);
        Assert.Equal(1, guard.Leases[0].Commits);
        Assert.Equal(1, guard.Leases[1].Commits);
        Assert.Equal(0, guard.Leases[1].Abandons);
    }

    [Fact]
    public async Task Guarded_deactivation_abandons_when_restore_fails()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, display, _, _, _) = Create(rescueGuard: guard);
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        display.RestoreException = new InvalidOperationException("restore failed");

        await coordinator.SetEnabledAsync(false);

        Assert.Equal(2, guard.Leases.Count);
        Assert.Equal(1, guard.Leases[1].Abandons);
        Assert.Equal(AutomationState.Error, coordinator.State);
    }

    [Fact]
    public async Task Guard_begin_failure_prevents_apply()
    {
        var guard = new FakeRescueGuard
        {
            BeginException = new InvalidOperationException("watchdog unavailable"),
        };
        var (coordinator, display, _, _, journal) = Create(rescueGuard: guard);
        await coordinator.SetEnabledAsync(true);

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Empty(display.Applied);
        Assert.Empty(guard.Leases);
        Assert.Null(journal.Entry);
    }

    [Fact]
    public async Task Watchdog_loss_before_apply_prevents_mutation_and_keeps_journal()
    {
        var guard = new FakeRescueGuard { FailImmediately = true };
        var (coordinator, display, _, _, journal) = Create(rescueGuard: guard);
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Empty(display.Applied);
        Assert.Equal(SessionJournalEntry.PendingState, journal.Entry?.State);
        Assert.Equal(1, Assert.Single(guard.Leases).Abandons);
    }

    [Fact]
    public async Task Watchdog_loss_during_non_cancellable_apply_waits_then_restores()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, display, _, _, journal) = Create(rescueGuard: guard);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        display.DuringApply = async () => { entered.SetResult(); await release.Task; };
        await coordinator.SetEnabledAsync(true);
        var activation = coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(guard.Leases).Fail();
        Assert.Empty(display.Restored);
        release.SetResult();
        await activation;

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Single(display.Restored);
        Assert.Equal(SessionJournalEntry.PendingState, journal.Entry?.State);
        Assert.Equal(1, guard.Leases[0].Abandons);
    }

    [Fact]
    public async Task Watchdog_loss_after_apply_restores_and_keeps_journal()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, display, _, _, journal) = Create(rescueGuard: guard);
        display.AfterApply = () => Assert.Single(guard.Leases).Fail();
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Single(display.Restored);
        Assert.Equal(SessionJournalEntry.PendingState, journal.Entry?.State);
    }

    [Fact]
    public async Task Watchdog_loss_during_rollback_keeps_journal()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, display, _, _, journal) = Create(rescueGuard: guard);
        display.ApplyException = new InvalidOperationException("apply failed");
        display.AfterRestore = () => Assert.Single(guard.Leases).Fail();
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Equal(SessionJournalEntry.PendingState, journal.Entry?.State);
    }

    [Fact]
    public async Task Watchdog_loss_before_deactivation_keeps_snapshot_and_journal()
    {
        var guard = new FakeRescueGuard();
        var (coordinator, _, _, _, journal) = Create(rescueGuard: guard);
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);
        guard.FailImmediately = true;
        await coordinator.SetEnabledAsync(false);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.NotNull(coordinator.Snapshot);
        Assert.NotNull(journal.Entry);
        Assert.Equal(1, guard.Leases[1].Abandons);
    }

    [Fact]
    public async Task Guard_abort_failure_falls_back_to_abandon()
    {
        var guard = new FakeRescueGuard
        {
            AbortException = new InvalidOperationException("abort failed"),
        };
        var (coordinator, display, _, _, _) = Create(rescueGuard: guard);
        display.ApplyException = new InvalidOperationException("apply failed");
        await coordinator.SetEnabledAsync(true);

        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        var lease = Assert.Single(guard.Leases);
        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Equal(1, lease.Aborts);
        Assert.Equal(1, lease.Abandons);
    }

    [Fact]
    public async Task Uncertain_commit_preserves_journal_and_blocks_further_mutations()
    {
        var guard = new FakeRescueGuard { CommitException = new RescueDecisionUncertainException("uncertain") };
        var (coordinator, display, _, _, journal) = Create(rescueGuard: guard);
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Single(display.Applied);
        Assert.Empty(display.Restored);
        Assert.NotNull(journal.Entry);
        await coordinator.SetEnabledAsync(false);
        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Empty(display.Restored);
        Assert.NotNull(journal.Entry);
    }

    [Fact]
    public async Task Uncertain_abort_preserves_journal_and_blocks_further_mutations()
    {
        var guard = new FakeRescueGuard { AbortException = new RescueDecisionUncertainException("uncertain") };
        var (coordinator, display, _, _, journal) = Create(rescueGuard: guard);
        display.ApplyException = new InvalidOperationException("apply failed");
        await coordinator.SetEnabledAsync(true);
        await coordinator.GamepadConnectedAsync(GamepadDevice.Simulated);

        Assert.Equal(AutomationState.Error, coordinator.State);
        Assert.Single(display.Restored);
        Assert.NotNull(journal.Entry);
        await coordinator.SetEnabledAsync(false);
        Assert.Single(display.Restored);
        Assert.NotNull(journal.Entry);
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
        FakeJournal Journal) Create(
        DisplayState? baseline = null,
        IRescueGuard? rescueGuard = null)
    {
        var display = new FakeDisplayService();
        if (baseline is not null)
        {
            display.Current = baseline;
        }

        var audio = new FakeAudioService();
        var delay = new ManualDelayScheduler();
        var journal = new FakeJournal();
        var coordinator = new AutomationCoordinator(
            display,
            audio,
            delay,
            journal,
            rescueGuard: rescueGuard);

        return (coordinator, display, audio, delay, journal);
    }

    private sealed class FakeDisplayService : IDisplayService
    {
        public DisplayState Current { get; set; } =
            new(DisplayMode.InternalOnly, PrimaryActive: true, TvActive: false);

        public List<DisplayMode> Applied { get; } = new();

        public List<DisplayState> Restored { get; } = new();

        public Exception? ApplyException { get; set; }

        public Exception? RestoreException { get; set; }

        public Func<Task>? DuringApply { get; set; }

        public Action? AfterApply { get; set; }

        public Action? AfterRestore { get; set; }

        public DisplayState GetCurrent() => Current;

        public async Task<DisplayState> ApplyAsync(
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

            if (DuringApply is not null) await DuringApply();
            AfterApply?.Invoke();
            return Current;
        }

        public Task RestoreAsync(
            DisplayState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (RestoreException is not null)
            {
                throw RestoreException;
            }

            Restored.Add(state);
            Current = state;
            AfterRestore?.Invoke();
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

    private sealed class FakeRescueGuard : IRescueGuard
    {
        public List<FakeRescueLease> Leases { get; } = new();

        public Exception? BeginException { get; set; }

        public Exception? AbortException { get; set; }

        public Exception? CommitException { get; set; }

        public bool FailImmediately { get; set; }

        public Task<IRescueLease> BeginAsync(
            string operation,
            CancellationToken cancellationToken = default)
        {
            if (BeginException is not null)
            {
                throw BeginException;
            }

            var lease = new FakeRescueLease(operation)
            {
                AbortException = AbortException,
                CommitException = CommitException,
            };

            Leases.Add(lease);
            if (FailImmediately) lease.Fail();
            return Task.FromResult<IRescueLease>(lease);
        }
    }

    private sealed class FakeRescueLease : IRescueLease
    {
        public FakeRescueLease(string operation) => Operation = operation;

        public string Operation { get; }

        private readonly CancellationTokenSource _failure = new();

        public CancellationToken FailureToken => _failure.Token;

        public void Fail() => _failure.Cancel();

        public void ThrowIfUnavailable()
        {
            if (_failure.IsCancellationRequested) throw new InvalidOperationException("watchdog unavailable");
        }

        public int Commits { get; private set; }

        public int Aborts { get; private set; }

        public int Abandons { get; private set; }

        public int Disposals { get; private set; }

        public Exception? AbortException { get; set; }

        public Exception? CommitException { get; set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commits++;
            if (CommitException is not null) throw CommitException;
            return Task.CompletedTask;
        }

        public Task AbortAsync(CancellationToken cancellationToken = default)
        {
            Aborts++;

            if (AbortException is not null)
            {
                throw AbortException;
            }

            return Task.CompletedTask;
        }

        public void Abandon() => Abandons++;

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeJournal : ISessionJournalStore
    {
        public List<SessionJournalEntry> Saved { get; } = new();

        public SessionJournalEntry? Entry => Saved.Count > 0 ? Saved[^1] : null;

        public int Saves => Saved.Count;

        public int Clears { get; private set; }

        public int SaveAttempts { get; private set; }

        /// <summary>Número da tentativa (1-based) que deve falhar; 0 = nunca.</summary>
        public int FailOnSaveNumber { get; set; }

        public SessionJournalEntry? Load() => Entry;

        public void Save(SessionJournalEntry entry)
        {
            SaveAttempts++;

            if (FailOnSaveNumber == SaveAttempts)
            {
                throw new IOException("journal unavailable");
            }

            Saved.Add(entry);
        }

        public void Clear()
        {
            Saved.Clear();
            Clears++;
        }
    }
}

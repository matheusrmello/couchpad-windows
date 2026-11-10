using CouchPad.Core.Audio;
using CouchPad.Core.Devices;
using CouchPad.Core.Display;

namespace CouchPad.Core.Automation;

/// <summary>
/// State machine da automação. Reage a conectar/desconectar do gamepad,
/// aplica display/áudio depois dos atrasos e restaura no fim.
/// Nenhuma chamada de hardware: depende de IDisplayService/IAudioService.
/// </summary>
public sealed class AutomationCoordinator
{
    private readonly IDisplayService _display;
    private readonly IAudioService _audio;
    private readonly IDelayScheduler _delay;
    private readonly ISessionJournalStore? _journal;
    private readonly IRescueGuard? _rescueGuard;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();

    private CancellationTokenSource? _cts;
    private GamepadDevice? _gamepad;
    private AutomationSnapshot? _snapshot;
    private AutomationState _state = AutomationState.Idle;
    private string? _lastError;
    private bool _recoveryUncertain;

    public AutomationCoordinator(
        IDisplayService display,
        IAudioService audio,
        IDelayScheduler delay,
        ISessionJournalStore? journal = null,
        TimeProvider? timeProvider = null,
        IRescueGuard? rescueGuard = null)
    {
        if (rescueGuard is not null && journal is null)
            throw new ArgumentException("A rescue guard requires a journal.", nameof(journal));
        _display = display ?? throw new ArgumentNullException(nameof(display));
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _journal = journal;
        _time = timeProvider ?? TimeProvider.System;
        _rescueGuard = rescueGuard;
    }

    public AutomationSettings Settings { get; private set; } = new();

    public AutomationState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public string? LastError
    {
        get
        {
            lock (_sync)
            {
                return _lastError;
            }
        }
    }

    public bool IsRecoveryUncertain
    {
        get
        {
            lock (_sync)
            {
                return _recoveryUncertain;
            }
        }
    }

    public AutomationSnapshot? Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    public GamepadDevice? Gamepad
    {
        get
        {
            lock (_sync)
            {
                return _gamepad;
            }
        }
    }

    /// <summary>
    /// Handlers não devem chamar métodos do coordenador de forma síncrona;
    /// use as APIs assíncronas para evitar reentrância no gate.
    /// </summary>
    public event EventHandler<AutomationStateChangedEventArgs>? StateChanged;

    public Task SetEnabledAsync(bool enabled)
    {
        var settings = Settings;
        return UpdateSettingsAsync(settings with { Enabled = enabled });
    }

    /// <summary>
    /// Atualiza a configuração. Ligar/desligar e trocar modos enquanto ativo
    /// disparam uma nova transição; mudar atraso vale para a próxima.
    /// </summary>
    public async Task UpdateSettingsAsync(AutomationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AutomationSettings previous;
        lock (_sync)
        {
            previous = Settings;
            Settings = settings;
        }

        if (previous.Enabled != settings.Enabled)
        {
            if (!settings.Enabled)
            {
                await RunAsync(token => DeactivateAsync(token, immediate: true));
            }
            else
            {
                await RunAsync(async token =>
                {
                    if (Gamepad is null)
                    {
                        SetState(AutomationState.WaitingForGamepad);
                        return;
                    }

                    await ActivateAsync(token);
                });
            }

            return;
        }

        if (settings.Enabled &&
            State == AutomationState.Active &&
            (previous.DisplayMode != settings.DisplayMode || previous.AudioMode != settings.AudioMode))
        {
            await RunAsync(token => ActivateAsync(token, immediate: true));
        }
    }

    public async Task GamepadConnectedAsync(GamepadDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        lock (_sync)
        {
            _gamepad = device;
        }

        var settings = Settings;
        if (!settings.Enabled)
        {
            SetState(AutomationState.Idle);
            return;
        }

        if (State is AutomationState.Active or AutomationState.Applying)
        {
            return;
        }

        await RunAsync(token => ActivateAsync(token));
    }

    public async Task GamepadDisconnectedAsync()
    {
        lock (_sync)
        {
            _gamepad = null;
        }

        var settings = Settings;
        if (!settings.Enabled)
        {
            await RunAsync(token =>
            {
                SetState(AutomationState.Idle);
                return Task.CompletedTask;
            });
            return;
        }

        if (Snapshot is null)
        {
            await RunAsync(token =>
            {
                SetState(AutomationState.WaitingForGamepad);
                return Task.CompletedTask;
            });
            return;
        }

        await RunAsync(token => DeactivateAsync(token));
    }

    public async Task ResetAsync()
    {
        lock (_sync)
        {
            _gamepad = null;
            Settings = Settings with { Enabled = false };
        }

        await RunAsync(token => DeactivateAsync(token, immediate: true));
    }

    private async Task ActivateAsync(CancellationToken cancellationToken, bool immediate = false)
    {
        var settings = Settings;
        var delay = immediate ? TimeSpan.Zero : settings.ActivationDelay;
        if (delay > TimeSpan.Zero)
        {
            SetState(AutomationState.Pending);
            await _delay.DelayAsync(delay, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!settings.Enabled || Gamepad is null)
        {
            SetState(settings.Enabled ? AutomationState.WaitingForGamepad : AutomationState.Idle);
            return;
        }

        var previousSnapshot = Snapshot;
        var displayBefore = _display.GetCurrent();
        var audioBefore = _audio.GetCurrent();
        var restoreDisplay = previousSnapshot?.RestoreDisplay ?? displayBefore;
        var restoreAudio = previousSnapshot?.RestoreAudio ?? audioBefore;

        var pending = new SessionJournalEntry(
            SessionJournalEntry.PendingState,
            settings.DisplayMode,
            settings.AudioMode,
            _time.GetUtcNow())
        {
            RestoreDisplay = restoreDisplay,
            RestoreAudio = restoreAudio,
        };

        // Every apply requires a confirmed pending journal, including reapply.
        if (!TrySaveJournal(pending))
        {
            SetState(AutomationState.Error, "journal unavailable");
            return;
        }

        IRescueLease? lease = null;

        try
        {
            if (_rescueGuard is not null)
            {
                lease = await _rescueGuard.BeginAsync("display apply", cancellationToken);
            }

            lease?.ThrowIfUnavailable();
            using var guardedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, lease?.FailureToken ?? CancellationToken.None);
            var guardedToken = guardedCancellation.Token;
            SetState(AutomationState.Applying);

            guardedToken.ThrowIfCancellationRequested();
            lease?.ThrowIfUnavailable();
            await _display.ApplyAsync(settings.DisplayMode, guardedToken);
            lease?.ThrowIfUnavailable();
            guardedToken.ThrowIfCancellationRequested();
            var audio = await _audio.ApplyAsync(settings.AudioMode, guardedToken);
            lease?.ThrowIfUnavailable();
            guardedToken.ThrowIfCancellationRequested();

            var snapshot = new AutomationSnapshot(
                restoreDisplay,
                restoreAudio,
                settings.DisplayMode,
                settings.AudioMode,
                audio);

            lock (_sync)
            {
                _snapshot = snapshot;
            }

            var active = new SessionJournalEntry(
                SessionJournalEntry.ActiveState,
                settings.DisplayMode,
                settings.AudioMode,
                _time.GetUtcNow())
            {
                RestoreDisplay = restoreDisplay,
                RestoreAudio = restoreAudio,
                AppliedAudio = audio,
            };

            // Se esta gravação falhar, o pending anterior ainda contém o
            // baseline e serve para o rescue da P6.
            TrySaveJournal(active);

            if (lease is not null)
            {
                lease.ThrowIfUnavailable();
                await lease.CommitAsync(guardedToken);
            }

            SetState(AutomationState.Active);
        }
        catch (RescueDecisionUncertainException ex)
        {
            lease?.Abandon();
            lock (_sync) { _recoveryUncertain = true; }
            SetState(AutomationState.Error, ex.Message);
        }
        catch (Exception ex) when (lease?.FailureToken.IsCancellationRequested == true)
        {
            await TryRestoreAsync(displayBefore, audioBefore);
            lease.Abandon();
            lock (_sync)
            {
                _snapshot = previousSnapshot;
                _recoveryUncertain = true;
            }
            // Keep the recovery journal even when the immediate rollback succeeds.
            SetState(AutomationState.Error, ex.Message);
        }
        catch (OperationCanceledException)
        {
            var restored = await TryRestoreAsync(displayBefore, audioBefore);
            var released = await FinishLeaseAsync(lease, restored);

            lock (_sync)
            {
                _snapshot = previousSnapshot;
            }

            if (lease?.FailureToken.IsCancellationRequested == true || !released)
            {
                lock (_sync) { _recoveryUncertain = true; }
                SetState(AutomationState.Error, "watchdog unavailable");
                return;
            }

            if (restored && previousSnapshot is null)
            {
                TryClearJournal();
            }

            throw;
        }
        catch (Exception ex)
        {
            var restored = await TryRestoreAsync(displayBefore, audioBefore);
            var released = await FinishLeaseAsync(lease, restored);

            lock (_sync)
            {
                _snapshot = previousSnapshot;
            }

            if (!released || lease?.FailureToken.IsCancellationRequested == true)
            {
                lock (_sync) { _recoveryUncertain = true; }
            }

            if (restored && released && previousSnapshot is null && lease?.FailureToken.IsCancellationRequested != true)
            {
                TryClearJournal();
            }

            SetState(AutomationState.Error, ex.Message);
        }
        finally
        {
            await DisposeLeaseAsync(lease);
        }
    }

    private async Task DeactivateAsync(CancellationToken cancellationToken, bool immediate = false)
    {
        var settings = Settings;
        var snapshot = Snapshot;
        if (snapshot is null)
        {
            SetState(settings.Enabled ? AutomationState.WaitingForGamepad : AutomationState.Idle);
            return;
        }

        var delay = immediate ? TimeSpan.Zero : settings.DeactivationDelay;
        if (delay > TimeSpan.Zero)
        {
            SetState(AutomationState.Pending);
            await _delay.DelayAsync(delay, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        IRescueLease? lease = null;

        try
        {
            if (_rescueGuard is not null)
            {
                lease = await _rescueGuard.BeginAsync("display restore", cancellationToken);
            }

            lease?.ThrowIfUnavailable();
            using var guardedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, lease?.FailureToken ?? CancellationToken.None);
            var guardedToken = guardedCancellation.Token;
            SetState(AutomationState.Applying);

            guardedToken.ThrowIfCancellationRequested();
            lease?.ThrowIfUnavailable();
            await _display.RestoreAsync(snapshot.RestoreDisplay, guardedToken);
            lease?.ThrowIfUnavailable();
            guardedToken.ThrowIfCancellationRequested();
            await _audio.RestoreAsync(snapshot.RestoreAudio, guardedToken);
            lease?.ThrowIfUnavailable();
            guardedToken.ThrowIfCancellationRequested();

            if (lease is not null)
            {
                await lease.CommitAsync(guardedToken);
            }

            lock (_sync)
            {
                _snapshot = null;
            }

            TryClearJournal();
        }
        catch (RescueDecisionUncertainException ex)
        {
            lease?.Abandon();
            lock (_sync) { _recoveryUncertain = true; }
            SetState(AutomationState.Error, ex.Message);
            return;
        }
        catch (Exception ex) when (lease?.FailureToken.IsCancellationRequested == true)
        {
            await TryRestoreAsync(snapshot.RestoreDisplay, snapshot.RestoreAudio);
            lease.Abandon();
            lock (_sync) { _recoveryUncertain = true; }
            SetState(AutomationState.Error, ex.Message);
            return;
        }
        catch (OperationCanceledException)
        {
            lease?.Abandon();
            if (lease is not null)
            {
                lock (_sync) { _recoveryUncertain = true; }
                SetState(AutomationState.Error, "recovery outcome uncertain");
                return;
            }
            throw;
        }
        catch (Exception ex)
        {
            lease?.Abandon();
            if (lease is not null) lock (_sync) { _recoveryUncertain = true; }
            SetState(AutomationState.Error, ex.Message);
            return;
        }
        finally
        {
            await DisposeLeaseAsync(lease);
        }

        SetState(settings.Enabled ? AutomationState.WaitingForGamepad : AutomationState.Idle);
    }

    private async Task RunAsync(Func<CancellationToken, Task> body)
    {
        var cts = new CancellationTokenSource();

        lock (_sync)
        {
            _cts?.Cancel();
            _cts = cts;
        }

        await _gate.WaitAsync();
        try
        {
            if (cts.IsCancellationRequested)
            {
                return;
            }

            bool blocked;
            lock (_sync) { blocked = _recoveryUncertain; }
            if (blocked)
            {
                SetState(AutomationState.Error, "recovery outcome uncertain");
                return;
            }

            await body(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Uma operação mais nova assumiu; o estado será atualizado por ela.
        }
        catch (Exception ex)
        {
            SetState(AutomationState.Error, ex.Message);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                }
            }

            _gate.Release();
            cts.Dispose();
        }
    }

    private void SetState(AutomationState state, string? error = null)
    {
        AutomationStateChangedEventArgs args;

        lock (_sync)
        {
            _state = state;
            _lastError = state == AutomationState.Error ? error : null;
            args = new AutomationStateChangedEventArgs(state, _lastError, _recoveryUncertain);
        }

        StateChanged?.Invoke(this, args);
    }

    private static async Task<bool> FinishLeaseAsync(IRescueLease? lease, bool restored)
    {
        if (lease is null)
        {
            return true;
        }

        try
        {
            if (restored)
            {
                await lease.AbortAsync(CancellationToken.None);
                return true;
            }
            else
            {
                lease.Abandon();
                return false;
            }
        }
        catch
        {
            lease.Abandon();
            return false;
        }
    }

    private static async Task DisposeLeaseAsync(IRescueLease? lease)
    {
        if (lease is null)
        {
            return;
        }

        try
        {
            await lease.DisposeAsync();
        }
        catch
        {
            // Dispose do lease não pode mascarar o resultado da operação.
        }
    }

    private async Task<bool> TryRestoreAsync(DisplayState display, AudioOutput audio)
    {
        var restored = true;

        try
        {
            await _display.RestoreAsync(display, CancellationToken.None);
        }
        catch
        {
            restored = false;
        }

        try
        {
            await _audio.RestoreAsync(audio, CancellationToken.None);
        }
        catch
        {
            restored = false;
        }

        return restored;
    }

    private bool TrySaveJournal(SessionJournalEntry entry)
    {
        if (_journal is null)
        {
            return true;
        }

        try
        {
            _journal.Save(entry);
            return true;
        }
        catch
        {
            // O chamador decide se a falha é fatal (pending inicial) ou não.
            return false;
        }
    }

    private void TryClearJournal()
    {
        if (_journal is null)
        {
            return;
        }

        try
        {
            _journal.Clear();
        }
        catch
        {
            // Limpeza é melhor esforço.
        }
    }
}

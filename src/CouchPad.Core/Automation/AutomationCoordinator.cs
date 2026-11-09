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
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();

    private CancellationTokenSource? _cts;
    private GamepadDevice? _gamepad;
    private AutomationSnapshot? _snapshot;
    private AutomationState _state = AutomationState.Idle;
    private string? _lastError;

    public AutomationCoordinator(
        IDisplayService display,
        IAudioService audio,
        IDelayScheduler delay,
        ISessionJournalStore? journal = null,
        TimeProvider? timeProvider = null)
    {
        _display = display ?? throw new ArgumentNullException(nameof(display));
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _journal = journal;
        _time = timeProvider ?? TimeProvider.System;
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

        SetState(AutomationState.Applying);

        var previousSnapshot = Snapshot;
        var displayBefore = _display.GetCurrent();
        var audioBefore = _audio.GetCurrent();
        var restoreDisplay = previousSnapshot?.RestoreDisplay ?? displayBefore;
        var restoreAudio = previousSnapshot?.RestoreAudio ?? audioBefore;

        try
        {
            await _display.ApplyAsync(settings.DisplayMode, cancellationToken);
            var audio = await _audio.ApplyAsync(settings.AudioMode, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

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

            TrySaveJournal(snapshot);
            SetState(AutomationState.Active);
        }
        catch (OperationCanceledException)
        {
            await TryRestoreAsync(displayBefore, audioBefore);

            lock (_sync)
            {
                _snapshot = previousSnapshot;
            }

            throw;
        }
        catch (Exception ex)
        {
            await TryRestoreAsync(displayBefore, audioBefore);

            lock (_sync)
            {
                _snapshot = previousSnapshot;
            }

            if (previousSnapshot is null)
            {
                TryClearJournal();
            }

            SetState(AutomationState.Error, ex.Message);
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
        SetState(AutomationState.Applying);

        try
        {
            await _display.RestoreAsync(snapshot.RestoreDisplay, cancellationToken);
            await _audio.RestoreAsync(snapshot.RestoreAudio, cancellationToken);

            lock (_sync)
            {
                _snapshot = null;
            }

            TryClearJournal();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetState(AutomationState.Error, ex.Message);
            return;
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
            var effectiveError = state == AutomationState.Error ? error : null;
            if (_state == state && _lastError == effectiveError)
            {
                return;
            }

            _state = state;
            _lastError = effectiveError;
            args = new AutomationStateChangedEventArgs(state, effectiveError);
        }

        StateChanged?.Invoke(this, args);
    }

    private async Task TryRestoreAsync(DisplayState display, AudioOutput audio)
    {
        try
        {
            await _display.RestoreAsync(display, CancellationToken.None);
        }
        catch
        {
            // Rollback é melhor esforço; o erro original é preservado.
        }

        try
        {
            await _audio.RestoreAsync(audio, CancellationToken.None);
        }
        catch
        {
            // Rollback é melhor esforço; o erro original é preservado.
        }
    }

    private void TrySaveJournal(AutomationSnapshot snapshot)
    {
        if (_journal is null)
        {
            return;
        }

        try
        {
            _journal.Save(new SessionJournalEntry(
                SessionJournalEntry.ActiveState,
                snapshot.DisplayMode,
                snapshot.AudioMode,
                _time.GetUtcNow())
            {
                RestoreDisplay = snapshot.RestoreDisplay,
                RestoreAudio = snapshot.RestoreAudio,
            });
        }
        catch
        {
            // Journal não pode derrubar a automação.
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

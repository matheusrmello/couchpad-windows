using CouchPad.Core.Devices;
using CouchPad.Core.Performance;

namespace CouchPad.App.Performance;

/// <summary>Owns the HUD sampling loop, runtime visibility and controller chord.</summary>
public sealed class PerformanceHudCoordinator : IPerformanceHudController, IDisposable
{
    private readonly IPerformanceSampler _sampler;
    private readonly IGamepadButtonSource _buttons;
    private readonly IPerformanceHudPresenter _presenter;
    private readonly TimeProvider _time;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _stop = new();
    private PerformanceHudSettings _settings;
    private GamepadChordDetector _chord;
    private PerformanceSnapshot _last = PerformanceSnapshot.Empty;
    private bool _started;
    private bool _visible = true;
    private bool _sessionActive;
    private bool _disposed;

    public PerformanceHudCoordinator(
        IPerformanceSampler sampler,
        IGamepadButtonSource buttons,
        IPerformanceHudPresenter presenter,
        PerformanceHudSettings settings,
        TimeProvider? time = null)
    {
        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        _buttons = buttons ?? throw new ArgumentNullException(nameof(buttons));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _time = time ?? TimeProvider.System;
        _chord = new GamepadChordDetector(settings.Chord);
    }

    public bool Enabled
    {
        get { lock (_sync) return _settings.Enabled; }
    }

    public bool IsVisible => _presenter.IsVisible;

    public event EventHandler? Changed;

    public void Start()
    {
        lock (_sync)
        {
            if (_started || _disposed) return;
            _started = true;
            _buttons.ButtonsReported += OnButtonsReported;
        }
        _ = Task.Run(LoopAsync);
        UpdateVisibility();
    }

    public void ApplySettings(PerformanceHudSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_sync)
        {
            var wasEnabled = _settings.Enabled;
            _settings = settings;
            _chord = new GamepadChordDetector(settings.Chord);
            _visible = settings.Enabled && (!wasEnabled || _visible);
        }
        _presenter.ApplySettings(settings);
        UpdateVisibility();
        RaiseChanged();
    }

    public void SetEnabled(bool enabled)
    {
        PerformanceHudSettings settings;
        lock (_sync) settings = _settings with { Enabled = enabled };
        ApplySettings(settings);
    }

    public void Toggle()
    {
        lock (_sync)
        {
            if (!_settings.Enabled) return;
            _visible = !_visible;
        }
        UpdateVisibility();
        RaiseChanged();
    }

    public void SetSessionActive(bool active)
    {
        lock (_sync) _sessionActive = active;
        UpdateVisibility();
    }

    private void OnButtonsReported(object? sender, GamepadButtonsChangedEventArgs e)
    {
        bool fire;
        lock (_sync)
        {
            if (_disposed) return;
            fire = _chord.Update(e.Buttons, _time.GetUtcNow());
        }
        if (fire) Toggle();
    }

    private void UpdateVisibility()
    {
        bool show;
        PerformanceHudSettings settings;
        PerformanceSnapshot last;
        lock (_sync)
        {
            if (!_started || _disposed) return;
            settings = _settings;
            last = _last;
            show = settings.Enabled && _visible && (!settings.ShowOnlyInSession || _sessionActive);
        }

        if (show) _presenter.Show(last, settings);
        else _presenter.Hide();
    }

    private async Task LoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                PerformanceHudSettings settings;
                bool shouldSample;
                lock (_sync)
                {
                    settings = _settings;
                    shouldSample = _started && !_disposed && settings.Enabled && _visible &&
                        (!settings.ShowOnlyInSession || _sessionActive);
                }
                if (!shouldSample) continue;

                PerformanceSnapshot snapshot;
                try
                {
                    snapshot = await _sampler.SampleAsync(_stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    snapshot = PerformanceSnapshot.Empty;
                }

                lock (_sync) _last = snapshot;
                _presenter.Update(snapshot, settings);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _buttons.ButtonsReported -= OnButtonsReported;
        _stop.Cancel();
        if (_sampler is IDisposable sampler) sampler.Dispose();
        if (_buttons is IDisposable buttons) buttons.Dispose();
        _presenter.Hide();
        _stop.Dispose();
    }
}

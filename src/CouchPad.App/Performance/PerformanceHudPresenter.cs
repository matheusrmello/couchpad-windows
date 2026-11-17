using Avalonia;
using Avalonia.Threading;
using CouchPad.App.ViewModels;
using CouchPad.App.Views;
using CouchPad.Core.Performance;

namespace CouchPad.App.Performance;

/// <summary>Avalonia presenter for the floating HUD. Window creation is lazy and UI-thread bound.</summary>
public sealed class PerformanceHudPresenter : IPerformanceHudPresenter, IDisposable
{
    private const double DesignWidth = 300;
    private const double DesignHeight = 210;
    private readonly PerformanceHudViewModel _viewModel = new();
    private PerformanceHudOverlayWindow? _window;
    private PerformanceHudSettings _settings = PerformanceHudSettings.Default;

    public bool IsVisible => _window?.IsVisible == true;

    public void ApplySettings(PerformanceHudSettings settings)
    {
        _settings = settings;
        Dispatcher.UIThread.Post(() =>
        {
            _viewModel.ApplySettings(settings);
            if (_window is { IsVisible: true })
            {
                ApplyWindowSettings();
                PositionWindow();
            }
        });
    }

    public void Show(PerformanceSnapshot snapshot, PerformanceHudSettings settings)
    {
        _settings = settings;
        Dispatcher.UIThread.Post(() =>
        {
            EnsureWindow();
            _viewModel.ApplySettings(settings);
            _viewModel.Update(snapshot);
            ApplyWindowSettings();
            if (!_window!.IsVisible) _window.Show();
            PositionWindow();
        });
    }

    public void Update(PerformanceSnapshot snapshot, PerformanceHudSettings settings)
    {
        _settings = settings;
        Dispatcher.UIThread.Post(() => _viewModel.Update(snapshot));
    }

    public void Hide()
    {
        Dispatcher.UIThread.Post(() => _window?.Hide());
    }

    private void EnsureWindow()
    {
        if (_window is not null) return;
        _window = new PerformanceHudOverlayWindow { DataContext = _viewModel };
    }

    private void ApplyWindowSettings()
    {
        if (_window is null) return;
        _window.Width = DesignWidth * _settings.Scale;
        _window.Height = DesignHeight * _settings.Scale;
        _window.Opacity = _settings.Opacity;
    }

    private void PositionWindow()
    {
        if (_window is null) return;
        var screen = _window.Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var scaling = screen.Scaling;
        var width = (int)Math.Round(_window.Width * scaling);
        var height = (int)Math.Round(_window.Height * scaling);
        var margin = (int)Math.Round(24 * scaling);
        var x = _settings.Position switch
        {
            PerformanceHudPosition.TopLeft or PerformanceHudPosition.BottomLeft => area.X + margin,
            _ => area.X + area.Width - width - margin,
        };
        var y = _settings.Position switch
        {
            PerformanceHudPosition.BottomLeft or PerformanceHudPosition.BottomRight => area.Y + area.Height - height - margin,
            _ => area.Y + margin,
        };
        _window.Position = new PixelPoint(x, y);
    }

    public void Dispose()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _window?.Close();
            _window = null;
        });
    }
}

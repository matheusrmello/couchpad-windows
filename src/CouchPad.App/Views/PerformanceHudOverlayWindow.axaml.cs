using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CouchPad.Platform.Windows;

namespace CouchPad.App.Views;

public partial class PerformanceHudOverlayWindow : Window
{
    public PerformanceHudOverlayWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnOpened(object? sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = TryGetPlatformHandle();
        if (handle is not null) WindowsOverlayWindow.ApplyClickThrough(handle.Handle);
    }
}

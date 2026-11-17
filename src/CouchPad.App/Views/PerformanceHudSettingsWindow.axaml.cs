using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CouchPad.App.ViewModels;
using CouchPad.Core.Performance;

namespace CouchPad.App.Views;

public partial class PerformanceHudSettingsWindow : Window
{
    public PerformanceHudSettingsWindow() : this(PerformanceHudSettings.Default)
    {
    }

    public PerformanceHudSettingsWindow(PerformanceHudSettings settings)
    {
        InitializeComponent();
        DataContext = new PerformanceHudSettingsViewModel(settings);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PerformanceHudSettingsViewModel viewModel)
            Close(viewModel.ToSettings());
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}

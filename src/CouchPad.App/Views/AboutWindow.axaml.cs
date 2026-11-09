using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CouchPad.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this.FindControl<Button>("CloseButton")?.Focus();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

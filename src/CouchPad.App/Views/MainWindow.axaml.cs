using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CouchPad.App.ViewModels;

namespace CouchPad.App.Views;

public partial class MainWindow : Window
{
    private const double WideBreakpoint = 1000;
    private const double MediumBreakpoint = 720;

    private int _columns = -1;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyLayout(Bounds.Width);

        var aboutButton = this.FindControl<Button>("AboutButton");
        if (aboutButton is not null)
        {
            aboutButton.Click += OnAboutClick;
        }

        Closed += OnClosed;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.FolderPickerRequested -= OnFolderPickerRequested;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.FolderPickerRequested += OnFolderPickerRequested;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FolderPickerRequested -= OnFolderPickerRequested;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyLayout(Bounds.Width);
    }

    /// <summary>
    /// 3 colunas em ≥1000 px, 2 em 720–999 px e 1 abaixo de 720 px.
    /// Os cards são reposicionados por código para manter o XAML declarativo.
    /// </summary>
    private void ApplyLayout(double width)
    {
        var columns = width >= WideBreakpoint ? 3 : width >= MediumBreakpoint ? 2 : 1;
        if (columns == _columns)
        {
            return;
        }

        var grid = this.FindControl<Grid>("CardsGrid");
        if (grid is null)
        {
            return;
        }

        _columns = columns;

        grid.ColumnDefinitions.Clear();
        for (var i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        var rows = columns switch { 3 => 3, 2 => 4, _ => 7 };
        grid.RowDefinitions.Clear();
        for (var i = 0; i < rows; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        var cards = new (string Name, Control? Card)[]
        {
            ("AutomationCard", this.FindControl<Control>("AutomationCard")),
            ("DisplayCard", this.FindControl<Control>("DisplayCard")),
            ("GamepadCard", this.FindControl<Control>("GamepadCard")),
            ("ComboCard", this.FindControl<Control>("ComboCard")),
            ("SettingsCard", this.FindControl<Control>("SettingsCard")),
            ("TransitionCard", this.FindControl<Control>("TransitionCard")),
            ("LaunchersCard", this.FindControl<Control>("LaunchersCard")),
        };

        for (var index = 0; index < cards.Length; index++)
        {
            var (name, card) = cards[index];
            if (card is null)
            {
                continue;
            }

            var (row, column, rowSpan, columnSpan) = Placement(name, index, columns);

            Grid.SetRow(card, row);
            Grid.SetColumn(card, column);
            Grid.SetRowSpan(card, rowSpan);
            Grid.SetColumnSpan(card, columnSpan);
        }
    }

    private static (int Row, int Column, int RowSpan, int ColumnSpan) Placement(
        string name,
        int index,
        int columns)
    {
        if (columns == 1)
        {
            return (index, 0, 1, 1);
        }

        if (columns == 2)
        {
            return name switch
            {
                "AutomationCard" => (0, 0, 1, 1),
                "DisplayCard" => (0, 1, 1, 1),
                "GamepadCard" => (1, 0, 1, 1),
                "ComboCard" => (1, 1, 1, 1),
                "TransitionCard" => (2, 0, 1, 1),
                "LaunchersCard" => (2, 1, 1, 1),
                "SettingsCard" => (3, 0, 1, 2),
                _ => (index, 0, 1, 1),
            };
        }

        return name switch
        {
            "AutomationCard" => (0, 0, 1, 1),
            "DisplayCard" => (0, 1, 1, 2),
            "GamepadCard" => (1, 0, 1, 1),
            "ComboCard" => (1, 1, 1, 1),
            "SettingsCard" => (1, 2, 2, 1),
            "TransitionCard" => (2, 0, 1, 1),
            "LaunchersCard" => (2, 1, 1, 1),
            _ => (index, 0, 1, 1),
        };
    }

    private async void OnFolderPickerRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = _viewModel.ChooseFolderDialogTitle,
                AllowMultiple = false,
            });

            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _viewModel.CustomLauncherPath = path;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or OperationCanceledException)
        {
            // Diálogo cancelado ou seletor indisponível: mantém o valor atual.
        }
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { DataContext = DataContext };
        await about.ShowDialog(this);
    }
}

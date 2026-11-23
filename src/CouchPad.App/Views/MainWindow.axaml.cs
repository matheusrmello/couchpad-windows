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
    /// Masonry de 3 colunas em ≥1000 px, 2 em 720–999 px e 1 abaixo de 720 px.
    /// Os cards são movidos entre as colunas em código para manter o XAML declarativo.
    /// A ordem de adição define a ordem de Tab.
    /// </summary>
    private void ApplyLayout(double width)
    {
        var columns = width >= WideBreakpoint ? 3 : width >= MediumBreakpoint ? 2 : 1;
        if (columns == _columns)
        {
            return;
        }

        var grid = this.FindControl<Grid>("CardsGrid");
        var column0 = this.FindControl<StackPanel>("Column0");
        var column1 = this.FindControl<StackPanel>("Column1");
        var column2 = this.FindControl<StackPanel>("Column2");

        if (grid is null || column0 is null || column1 is null || column2 is null)
        {
            return;
        }

        var automationCard = this.FindControl<Control>("AutomationCard");
        var displayCard = this.FindControl<Control>("DisplayCard");
        var gamepadCard = this.FindControl<Control>("GamepadCard");
        var comboCard = this.FindControl<Control>("ComboCard");
        var countdownCard = this.FindControl<Control>("CountdownCard");
        var transitionCard = this.FindControl<Control>("TransitionCard");
        var launchersCard = this.FindControl<Control>("LaunchersCard");
        var settingsCard = this.FindControl<Control>("SettingsCard");
        var tvCard = this.FindControl<Control>("TvCard");

        _columns = columns;

        column0.Children.Clear();
        column1.Children.Clear();
        column2.Children.Clear();

        // Zera os índices antes de limpar as definições: com menos colunas que a
        // posição atual, o índice antigo deixaria a grade inconsistente.
        Grid.SetColumn(column0, 0);
        Grid.SetColumn(column1, 0);
        Grid.SetColumn(column2, 0);

        grid.ColumnDefinitions.Clear();
        for (var i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        Grid.SetColumn(column0, 0);
        column0.IsVisible = true;
        Grid.SetColumn(column1, columns >= 2 ? 1 : 0);
        column1.IsVisible = columns >= 2;
        Grid.SetColumn(column2, columns >= 3 ? 2 : 0);
        column2.IsVisible = columns >= 3;

        (StackPanel Panel, Control?[] Cards)[] layout = columns switch
        {
            3 => new[]
            {
                (column0, new[] { automationCard, countdownCard, launchersCard }),
                (column1, new[] { displayCard, gamepadCard, comboCard }),
                (column2, new[] { tvCard, transitionCard, settingsCard }),
            },
            2 => new[]
            {
                (column0, new[] { displayCard, automationCard, countdownCard, launchersCard }),
                (column1, new[] { tvCard, gamepadCard, comboCard, transitionCard, settingsCard }),
            },
            _ => new[]
            {
                (column0, new[]
                {
                    automationCard,
                    displayCard,
                    tvCard,
                    gamepadCard,
                    comboCard,
                    countdownCard,
                    transitionCard,
                    launchersCard,
                    settingsCard,
                }),
            },
        };

        foreach (var (panel, cards) in layout)
        {
            foreach (var card in cards)
            {
                if (card is not null)
                {
                    panel.Children.Add(card);
                }
            }
        }
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

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace CouchPad.App.Controls;

/// <summary>
/// Caminho somente leitura + botão para escolher a pasta do launcher.
/// </summary>
public class PathPicker : TemplatedControl
{
    public static readonly StyledProperty<string?> PathTextProperty =
        AvaloniaProperty.Register<PathPicker, string?>(nameof(PathText));

    public static readonly StyledProperty<string?> ChooseTextProperty =
        AvaloniaProperty.Register<PathPicker, string?>(nameof(ChooseText));

    public static readonly StyledProperty<ICommand?> ChooseCommandProperty =
        AvaloniaProperty.Register<PathPicker, ICommand?>(nameof(ChooseCommand));

    public string? PathText
    {
        get => GetValue(PathTextProperty);
        set => SetValue(PathTextProperty, value);
    }

    public string? ChooseText
    {
        get => GetValue(ChooseTextProperty);
        set => SetValue(ChooseTextProperty, value);
    }

    public ICommand? ChooseCommand
    {
        get => GetValue(ChooseCommandProperty);
        set => SetValue(ChooseCommandProperty, value);
    }
}

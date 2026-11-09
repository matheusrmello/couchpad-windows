using Avalonia;
using Avalonia.Controls.Primitives;

namespace CouchPad.App.Controls;

/// <summary>Tecla da combinação de ativação.</summary>
public class KeyChip : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<KeyChip, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}

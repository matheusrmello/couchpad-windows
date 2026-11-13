using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CouchPad.App.Controls;

public enum CardTone
{
    Normal,
    Attention,
    Error,
}

/// <summary>
/// Card do shell: título, ação opcional no cabeçalho e conteúdo.
/// </summary>
public class Card : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Card, string?>(nameof(Title));

    public static readonly StyledProperty<Geometry?> TitleIconProperty =
        AvaloniaProperty.Register<Card, Geometry?>(nameof(TitleIcon));

    public static readonly StyledProperty<object?> HeaderActionProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(HeaderAction));

    public static readonly StyledProperty<CardTone> ToneProperty =
        AvaloniaProperty.Register<Card, CardTone>(nameof(Tone));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public Geometry? TitleIcon
    {
        get => GetValue(TitleIconProperty);
        set => SetValue(TitleIconProperty, value);
    }

    public object? HeaderAction
    {
        get => GetValue(HeaderActionProperty);
        set => SetValue(HeaderActionProperty, value);
    }

    public CardTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ToneProperty)
        {
            PseudoClasses.Set(":attention", Tone == CardTone.Attention);
            PseudoClasses.Set(":error", Tone == CardTone.Error);
        }
    }
}

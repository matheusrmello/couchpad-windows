using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace CouchPad.App.Controls;

/// <summary>
/// Linha de status: dot opcional, rótulo e valor. O valor é sempre textual.
/// </summary>
public class StatusRow : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatusRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatusRow, string?>(nameof(Value));

    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<StatusRow, StatusTone>(nameof(Tone));

    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<StatusRow, bool>(nameof(ShowDot), defaultValue: true);

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public StatusTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool ShowDot
    {
        get => GetValue(ShowDotProperty);
        set => SetValue(ShowDotProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ToneProperty)
        {
            PseudoClasses.Set(":accent", Tone == StatusTone.Accent);
            PseudoClasses.Set(":warning", Tone == StatusTone.Warning);
            PseudoClasses.Set(":danger", Tone == StatusTone.Danger);
        }
    }
}

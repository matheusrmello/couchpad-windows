using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace CouchPad.App.Controls;

public enum StatusTone
{
    Neutral,
    Accent,
    Warning,
    Danger,
}

/// <summary>
/// Pill de estado textual. A cor nunca é o único indicador: o texto acompanha.
/// O texto interno é uma live region para leitores de tela.
/// </summary>
public class StatusPill : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusPill, string?>(nameof(Text));

    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<StatusPill, StatusTone>(nameof(Tone));

    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<StatusPill, bool>(nameof(ShowDot), defaultValue: true);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var text = e.NameScope.Find<TextBlock>("PART_Text");
        if (text is not null)
        {
            AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
        }
    }
}

using Avalonia;
using Avalonia.Controls;

namespace CouchPad.App.Controls;

public enum ActionVariant
{
    Primary,
    Secondary,
}

public enum ActionState
{
    Idle,
    Loading,
    Success,
    Error,
}

/// <summary>
/// Botão de ação com estados visuais explícitos (idle, carregando, sucesso e erro).
/// O texto continua vindo do catálogo; o estado só altera a apresentação.
/// </summary>
public class ActionButton : Button
{
    public static readonly StyledProperty<ActionVariant> VariantProperty =
        AvaloniaProperty.Register<ActionButton, ActionVariant>(nameof(Variant));

    public static readonly StyledProperty<ActionState> StateProperty =
        AvaloniaProperty.Register<ActionButton, ActionState>(nameof(State));

    public ActionButton()
    {
        PseudoClasses.Set(":primary", Variant == ActionVariant.Primary);
        PseudoClasses.Set(":secondary", Variant == ActionVariant.Secondary);
        PseudoClasses.Set(":loading", State == ActionState.Loading);
        PseudoClasses.Set(":success", State == ActionState.Success);
        PseudoClasses.Set(":error", State == ActionState.Error);
    }

    public ActionVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ActionState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(":primary", Variant == ActionVariant.Primary);
            PseudoClasses.Set(":secondary", Variant == ActionVariant.Secondary);
        }

        if (change.Property == StateProperty)
        {
            PseudoClasses.Set(":loading", State == ActionState.Loading);
            PseudoClasses.Set(":success", State == ActionState.Success);
            PseudoClasses.Set(":error", State == ActionState.Error);
        }
    }
}

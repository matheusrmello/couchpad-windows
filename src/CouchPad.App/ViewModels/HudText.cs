using CouchPad.Core.Devices;
using CouchPad.Core.Performance;

namespace CouchPad.App.ViewModels;

/// <summary>Mapeia valores do HUD para chaves de localização. Sem texto fixo.</summary>
internal static class HudText
{
    public static string PositionKey(PerformanceHudPosition position) => position switch
    {
        PerformanceHudPosition.TopLeft => "hud.position.short.top_left",
        PerformanceHudPosition.BottomLeft => "hud.position.short.bottom_left",
        PerformanceHudPosition.BottomRight => "hud.position.short.bottom_right",
        _ => "hud.position.short.top_right",
    };

    public static string ChordKey(string? chordKey) => GamepadChordPresets.Resolve(chordKey).Key switch
    {
        "LB+RB+X" => "hud.chord.preset.lbrbx",
        "LB+RB+Back" => "hud.chord.preset.lbrbback",
        _ => "hud.chord.preset.lbrby",
    };
}

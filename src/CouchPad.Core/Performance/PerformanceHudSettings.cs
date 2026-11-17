using CouchPad.Core.Devices;
using CouchPad.Core.Settings;

namespace CouchPad.Core.Performance;

public enum PerformanceHudPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public static class PerformanceHudPositionCatalog
{
    public static PerformanceHudPosition Default => PerformanceHudPosition.TopRight;

    public static string Key(PerformanceHudPosition position) => position switch
    {
        PerformanceHudPosition.TopLeft => "topLeft",
        PerformanceHudPosition.TopRight => "topRight",
        PerformanceHudPosition.BottomLeft => "bottomLeft",
        PerformanceHudPosition.BottomRight => "bottomRight",
        _ => "topRight",
    };

    public static PerformanceHudPosition Parse(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "topleft" or "top-left" => PerformanceHudPosition.TopLeft,
        "bottomleft" or "bottom-left" => PerformanceHudPosition.BottomLeft,
        "bottomright" or "bottom-right" => PerformanceHudPosition.BottomRight,
        _ => PerformanceHudPosition.TopRight,
    };
}

public sealed record PerformanceHudSettings(
    bool Enabled,
    IReadOnlyList<PerformanceMetric> Metrics,
    PerformanceHudPosition Position,
    double Opacity,
    double Scale,
    bool ShowOnlyInSession,
    string ChordKey)
{
    public static PerformanceHudSettings Default { get; } = new(
        false,
        PerformanceMetricCatalog.DefaultMetrics,
        PerformanceHudPositionCatalog.Default,
        0.9,
        1.0,
        true,
        GamepadChordPresets.DefaultKey);

    public static PerformanceHudSettings FromAppSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.PerformanceHudEnabled,
            PerformanceMetricCatalog.Parse(settings.PerformanceHudMetrics),
            PerformanceHudPositionCatalog.Parse(settings.PerformanceHudPosition),
            Math.Clamp(settings.PerformanceHudOpacity, 0.4, 1.0),
            Math.Clamp(settings.PerformanceHudScale, 0.75, 1.5),
            settings.PerformanceHudShowOnlyInSession,
            GamepadChordPresets.Resolve(settings.PerformanceHudToggleChord).Key);
    }

    public AppSettings ApplyTo(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with
        {
            PerformanceHudEnabled = Enabled,
            PerformanceHudMetrics = PerformanceMetricCatalog.ToKeys(Metrics).ToList(),
            PerformanceHudPosition = PerformanceHudPositionCatalog.Key(Position),
            PerformanceHudOpacity = Math.Clamp(Opacity, 0.4, 1.0),
            PerformanceHudScale = Math.Clamp(Scale, 0.75, 1.5),
            PerformanceHudShowOnlyInSession = ShowOnlyInSession,
            PerformanceHudToggleChord = GamepadChordPresets.Resolve(ChordKey).Key,
        };
    }

    public GamepadChord Chord
    {
        get
        {
            var preset = GamepadChordPresets.Resolve(ChordKey);
            return new GamepadChord(preset.Buttons, preset.Hold);
        }
    }
}

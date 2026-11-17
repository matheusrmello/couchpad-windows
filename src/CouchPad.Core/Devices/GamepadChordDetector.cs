namespace CouchPad.Core.Devices;

public sealed record GamepadChord(GamepadButtons Buttons, TimeSpan Hold);

public sealed record GamepadChordPreset(string Key, GamepadButtons Buttons, TimeSpan Hold);

public static class GamepadChordPresets
{
    public const string DefaultKey = "LB+RB+Y";

    public static IReadOnlyList<GamepadChordPreset> All { get; } =
    [
        new("LB+RB+Y", GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.Y, TimeSpan.FromMilliseconds(800)),
        new("LB+RB+X", GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.X, TimeSpan.FromMilliseconds(800)),
        new("LB+RB+Back", GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.Back, TimeSpan.FromMilliseconds(800)),
    ];

    public static GamepadChordPreset Resolve(string? key)
    {
        var match = All.FirstOrDefault(preset => string.Equals(preset.Key, key, StringComparison.OrdinalIgnoreCase));
        return match ?? All[0];
    }
}

/// <summary>Edge-triggered detector for a held controller chord. Pure and testable.</summary>
public sealed class GamepadChordDetector
{
    private readonly GamepadChord _chord;
    private readonly TimeSpan _cooldown;
    private DateTimeOffset? _pressedAt;
    private DateTimeOffset _cooldownUntil;
    private bool _fired;

    public GamepadChordDetector(GamepadChord chord, TimeSpan? cooldown = null)
    {
        if (chord.Buttons == GamepadButtons.None) throw new ArgumentException("Chord must contain at least one button.", nameof(chord));
        if (chord.Hold <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(chord));
        _chord = chord;
        _cooldown = cooldown is { } value && value > TimeSpan.Zero ? value : TimeSpan.FromSeconds(1);
    }

    public bool Update(GamepadButtons pressed, DateTimeOffset now)
    {
        var exactChord = pressed == _chord.Buttons;
        if (!exactChord)
        {
            _pressedAt = null;
            _fired = false;
            return false;
        }

        if (_pressedAt is null)
        {
            _pressedAt = now;
            return false;
        }

        if (_fired || now < _cooldownUntil) return false;
        if (now - _pressedAt.Value < _chord.Hold) return false;
        _fired = true;
        _cooldownUntil = now + _cooldown;
        return true;
    }

    public void Reset()
    {
        _pressedAt = null;
        _fired = false;
        _cooldownUntil = default;
    }
}

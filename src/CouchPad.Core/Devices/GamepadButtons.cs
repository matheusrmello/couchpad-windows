namespace CouchPad.Core.Devices;

[Flags]
public enum GamepadButtons
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftShoulder = 1 << 4,
    RightShoulder = 1 << 5,
    Back = 1 << 6,
    Start = 1 << 7,
}

public sealed class GamepadButtonsChangedEventArgs : EventArgs
{
    public GamepadButtonsChangedEventArgs(GamepadButtons buttons) => Buttons = buttons;

    public GamepadButtons Buttons { get; }
}

/// <summary>Read-only source of controller button state; it never writes to the controller.</summary>
public interface IGamepadButtonSource : IDisposable
{
    /// <summary>
    /// Raised on every poll cycle with the current state, even when it repeats the previous one.
    /// Edge/level and hold detection are the consumer's responsibility.
    /// </summary>
    event EventHandler<GamepadButtonsChangedEventArgs>? ButtonsReported;
}

/// <summary>No-op source used by simulation and tests without a controller.</summary>
public sealed class NoopGamepadButtonSource : IGamepadButtonSource
{
    /// <inheritdoc />
    public event EventHandler<GamepadButtonsChangedEventArgs>? ButtonsReported
    {
        add { }
        remove { }
    }

    public void Dispose()
    {
    }
}

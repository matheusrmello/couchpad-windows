namespace CouchPad.Core.Devices;

/// <summary>Controle detectado. Na P3 o único dispositivo é simulado.</summary>
public sealed record GamepadDevice(string Id, string Name)
{
    public static GamepadDevice Simulated { get; } =
        new("simulated-gamepad", "Simulated controller");
}

public sealed class GamepadConnectionChangedEventArgs : EventArgs
{
    public GamepadConnectionChangedEventArgs(GamepadDevice? device) => Device = device;

    public GamepadDevice? Device { get; }
}

public interface IGamepadWatcher
{
    GamepadDevice? Current { get; }

    event EventHandler<GamepadConnectionChangedEventArgs>? ConnectionChanged;
}

/// <summary>Controle manual da fonte simulada; não existe em watcher real.</summary>
public interface ISimulatedGamepad : IGamepadWatcher
{
    void Connect(GamepadDevice? device = null);

    void Disconnect();
}

/// <summary>
/// Fonte simulada: conecta/desconecta sem hardware real.
/// </summary>
public sealed class SimulatedGamepadWatcher : ISimulatedGamepad
{
    public GamepadDevice? Current { get; private set; }

    public bool IsConnected => Current is not null;

    public event EventHandler<GamepadConnectionChangedEventArgs>? ConnectionChanged;

    public void Connect(GamepadDevice? device = null) => Set(device ?? GamepadDevice.Simulated);

    public void Disconnect() => Set(null);

    private void Set(GamepadDevice? device)
    {
        if (Equals(Current, device))
        {
            return;
        }

        Current = device;
        ConnectionChanged?.Invoke(this, new GamepadConnectionChangedEventArgs(device));
    }
}

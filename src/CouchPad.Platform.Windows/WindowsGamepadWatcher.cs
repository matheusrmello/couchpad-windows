using System.Runtime.InteropServices;
using CouchPad.Core.Devices;

namespace CouchPad.Platform.Windows;

/// <summary>Read-only XInput polling watcher. It never opens or changes controller state.</summary>
public sealed class WindowsGamepadWatcher : IGamepadWatcher, IDisposable
{
    private readonly IXInputStateSource _source;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private readonly object _sync = new();
    private GamepadDevice? _current;
    private bool _disposed;

    public WindowsGamepadWatcher(TimeSpan? pollInterval = null)
        : this(new NativeXInputStateSource(), pollInterval)
    {
    }

    internal WindowsGamepadWatcher(IXInputStateSource source, TimeSpan? pollInterval = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);
        if (_pollInterval < TimeSpan.FromMilliseconds(50) || _pollInterval > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        _current = Select(_source.GetConnectedSlots());
        _worker = Task.Run(PollAsync);
    }

    public GamepadDevice? Current { get { lock (_sync) return _current; } }

    public event EventHandler<GamepadConnectionChangedEventArgs>? ConnectionChanged;

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(_pollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                var next = Select(_source.GetConnectedSlots());
                EventHandler<GamepadConnectionChangedEventArgs>? handler;
                lock (_sync)
                {
                    if (_disposed || Equals(_current, next)) continue;
                    _current = next;
                    handler = ConnectionChanged;
                }
                handler?.Invoke(this, new GamepadConnectionChangedEventArgs(next));
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    internal static GamepadDevice? Select(IEnumerable<uint> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var slot = slots.Where(i => i < 4).Distinct().Order().Select(i => (uint?)i).FirstOrDefault();
        return slot is null ? null : new GamepadDevice($"xinput:{slot.Value}", "XInput gamepad");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _stop.Cancel();
        try { _worker.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        _stop.Dispose();
    }
}

internal interface IXInputStateSource
{
    IReadOnlyList<uint> GetConnectedSlots();
}

internal sealed class NativeXInputStateSource : IXInputStateSource
{
    public IReadOnlyList<uint> GetConnectedSlots()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var result = new List<uint>(4);
        for (uint slot = 0; slot < 4; slot++)
        {
            try
            {
                if (XInputGetState(slot, out _) == 0) result.Add(slot);
            }
            catch (DllNotFoundException) { throw new InvalidOperationException("Windows XInput is unavailable."); }
            catch (EntryPointNotFoundException) { throw new InvalidOperationException("Windows XInput is unavailable."); }
        }
        return result;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState", ExactSpelling = true)]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }
}

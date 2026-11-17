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
        _current = Select(_source.GetConnectedSlots(), _source.GetModelName);
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
                var next = Select(_source.GetConnectedSlots(), _source.GetModelName, _current);
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

    internal static GamepadDevice? Select(IEnumerable<uint> slots, Func<uint, string?>? getModelName = null,
        GamepadDevice? current = null)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var slot = slots.Where(i => i < 4).Distinct().Order().Select(i => (uint?)i).FirstOrDefault();
        if (slot is null) return null;
        var id = $"xinput:{slot.Value}";
        // Keep name lookup out of the poll loop while the selected slot stays connected.
        if (current?.Id == id) return current;
        return new GamepadDevice(id, getModelName?.Invoke(slot.Value) ?? "XInput gamepad");
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
    string? GetModelName(uint slot);
    bool TryGetButtons(uint slot, out ushort buttons);
}

internal sealed class NativeXInputStateSource : IXInputStateSource
{
    private static readonly Lazy<ReadExtendedCapabilities?> ExtendedCapabilities = new(ResolveExtendedCapabilities);

    public string? GetModelName(uint slot)
    {
        var read = ExtendedCapabilities.Value;
        if (read is null || read(1, slot, 0, out var capabilities) != 0) return null;
        return ModelName(capabilities.VendorId, capabilities.ProductId);
    }

    internal static string? ModelName(ushort vendor, ushort product) => (vendor, product) switch
    {
        (0x2DC8, 0x310B) => "8BitDo Ultimate 2 Wireless Controller",
        (0x2DC8, 0x310A) => "8BitDo Ultimate 2C Wireless Controller",
        _ => null,
    };

    private static ReadExtendedCapabilities? ResolveExtendedCapabilities()
    {
        // XInput 1.4 optionally exports the extended capabilities function at ordinal 108.
        var module = GetModuleHandle("xinput1_4.dll");
        var export = module == 0 ? 0 : GetProcAddress(module, 108);
        return export == 0 ? null : Marshal.GetDelegateForFunctionPointer<ReadExtendedCapabilities>(export);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint ReadExtendedCapabilities(uint reserved, uint slot, uint flags, out ExtendedControllerCapabilities capabilities);

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct ExtendedControllerCapabilities
    {
        [FieldOffset(20)] public ushort VendorId;
        [FieldOffset(22)] public ushort ProductId;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint GetModuleHandle(string moduleName);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetProcAddress(nint module, nint ordinal);

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

    public bool TryGetButtons(uint slot, out ushort buttons)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (XInputGetState(slot, out var state) != 0)
        {
            buttons = 0;
            return false;
        }
        buttons = state.Gamepad.Buttons;
        return true;
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

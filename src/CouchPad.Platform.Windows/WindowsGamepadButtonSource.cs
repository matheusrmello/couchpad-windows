using CouchPad.Core.Devices;

namespace CouchPad.Platform.Windows;

/// <summary>Read-only XInput button polling used for the HUD chord.</summary>
public sealed class WindowsGamepadButtonSource : IGamepadButtonSource
{
    private readonly IXInputStateSource _source;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private readonly object _sync = new();
    private GamepadButtons _current;
    private bool _disposed;

    public WindowsGamepadButtonSource(TimeSpan? pollInterval = null)
        : this(new NativeXInputStateSource(), pollInterval)
    {
    }

    internal WindowsGamepadButtonSource(IXInputStateSource source, TimeSpan? pollInterval = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
        if (_pollInterval < TimeSpan.FromMilliseconds(10) || _pollInterval > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        _worker = Task.Run(PollAsync);
    }

    public event EventHandler<GamepadButtonsChangedEventArgs>? ButtonsChanged;

    internal static GamepadButtons Map(ushort buttons)
    {
        var result = GamepadButtons.None;
        if ((buttons & 0x1000) != 0) result |= GamepadButtons.A;
        if ((buttons & 0x2000) != 0) result |= GamepadButtons.B;
        if ((buttons & 0x4000) != 0) result |= GamepadButtons.X;
        if ((buttons & 0x8000) != 0) result |= GamepadButtons.Y;
        if ((buttons & 0x0100) != 0) result |= GamepadButtons.LeftShoulder;
        if ((buttons & 0x0200) != 0) result |= GamepadButtons.RightShoulder;
        if ((buttons & 0x0020) != 0) result |= GamepadButtons.Back;
        if ((buttons & 0x0010) != 0) result |= GamepadButtons.Start;
        return result;
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(_pollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                var next = Read();
                EventHandler<GamepadButtonsChangedEventArgs>? handler;
                lock (_sync)
                {
                    if (_disposed || next == _current) continue;
                    _current = next;
                    handler = ButtonsChanged;
                }
                handler?.Invoke(this, new GamepadButtonsChangedEventArgs(next));
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    private GamepadButtons Read()
    {
        try
        {
            var slot = _source.GetConnectedSlots().Where(index => index < 4).Order().Select(index => (uint?)index).FirstOrDefault();
            if (slot is null || !_source.TryGetButtons(slot.Value, out var buttons)) return GamepadButtons.None;
            return Map(buttons);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            return GamepadButtons.None;
        }
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

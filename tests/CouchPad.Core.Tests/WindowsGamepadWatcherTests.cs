using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class WindowsGamepadWatcherTests
{
    [Fact]
    public void SelectReturnsNullWhenNoControllerIsConnected()
    {
        Assert.Null(WindowsGamepadWatcher.Select([]));
    }

    [Fact]
    public void SelectUsesFirstConnectedXInputSlotAndStableIdentity()
    {
        var selected = WindowsGamepadWatcher.Select([3, 1, 3]);

        Assert.NotNull(selected);
        Assert.Equal("xinput:1", selected.Id);
        Assert.Equal("XInput gamepad", selected.Name);
    }

    [Fact]
    public void SelectIgnoresOutOfRangeSlots()
    {
        Assert.Null(WindowsGamepadWatcher.Select([4, uint.MaxValue]));
    }
}

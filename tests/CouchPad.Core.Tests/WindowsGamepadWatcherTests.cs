using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class WindowsGamepadWatcherTests
{
    [Theory]
    [InlineData(0x2DC8, 0x310B, "8BitDo Ultimate 2 Wireless Controller")]
    [InlineData(0x2DC8, 0x310A, "8BitDo Ultimate 2C Wireless Controller")]
    [InlineData(0x2DC8, 0xFFFF, null)]
    [InlineData(0xFFFF, 0x310B, null)]
    public void Resolves_only_known_vendor_and_product_pairs(int vendor, int product, string? expected) =>
        Assert.Equal(expected, NativeXInputStateSource.ModelName((ushort)vendor, (ushort)product));

    [Fact]
    public void Looks_up_only_the_selected_slot_and_preserves_its_identity()
    {
        var selected = WindowsGamepadWatcher.Select([3, 1], slot =>
        {
            Assert.Equal(1u, slot);
            return "8BitDo Ultimate 2 Wireless Controller";
        });
        Assert.NotNull(selected);
        Assert.Equal("xinput:1", selected.Id);
        Assert.Equal("8BitDo Ultimate 2 Wireless Controller", selected.Name);
        Assert.Same(selected, WindowsGamepadWatcher.Select([1], _ => throw new InvalidOperationException(), selected));
    }

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

using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class PerMonitorDpiTests
{
    [Theory]
    [InlineData(100, 0)]
    [InlineData(125, 1)]
    [InlineData(150, 2)]
    [InlineData(175, 3)]
    [InlineData(200, 4)]
    public void ToDpiValue_maps_supported_percentages(int percent, int expected) =>
        Assert.Equal(expected, PerMonitorDpi.ToDpiValue(percent));

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(110)]
    [InlineData(176)]
    [InlineData(225)]
    [InlineData(-25)]
    public void ToDpiValue_rejects_unsupported_percentages(int percent) =>
        Assert.Equal(-1, PerMonitorDpi.ToDpiValue(percent));

    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, 100)]
    [InlineData(1, 125)]
    [InlineData(2, 150)]
    [InlineData(3, 175)]
    [InlineData(4, 200)]
    public void FromDpiValue_maps_stored_values(int value, int? expected) =>
        Assert.Equal(expected, PerMonitorDpi.FromDpiValue(value));

    [Theory]
    [InlineData(-2)]
    [InlineData(5)]
    public void FromDpiValue_rejects_out_of_range_values(int value) =>
        Assert.Null(PerMonitorDpi.FromDpiValue(value));
}

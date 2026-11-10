using System.Text.Json;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public class DisplayTopologySerializationTests
{
    [Fact]
    public void Snapshot_roundtrips_binary_data_and_target_identity()
    {
        var expected = new WindowsDisplayService.Snapshot(1, 2, 72, 64, Convert.ToBase64String(new byte[] { 0, 1, 255 }), Convert.ToBase64String(new byte[] { 5, 0 }), [new(1, -1, 3, "path", "name", 42, 12, 0)]);
        var actual = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(JsonSerializer.Serialize(expected));
        Assert.NotNull(actual);
        Assert.Equal(expected.PathsBase64, actual.PathsBase64);
        Assert.Equal(expected.ModesBase64, actual.ModesBase64);
        Assert.Equal(expected.Targets, actual.Targets);
    }
}

public class DisplaySelectionTests
{
    private static WindowsDisplayService.Target Target(uint id, string? path, string? name, ushort? manufacturer, ushort? product) => new(1, 0, id, path, name, manufacturer, product, 0);

    [Fact]
    public void Selects_identity_and_deduplicates_paths()
    {
        var target = Target(4, "path", "TV", 12, 34);
        Assert.Equal(target, WindowsDisplayService.SelectTarget([target, target], "PATH", 12, 34));
    }

    [Fact]
    public void Uses_exact_name_only_when_identity_is_missing()
    {
        var target = Target(4, null, "TV", null, null);
        Assert.Equal(target, WindowsDisplayService.SelectTarget([target], "path", 12, 34, "tv"));
        Assert.Throws<InvalidOperationException>(() => WindowsDisplayService.SelectTarget([Target(4, "other", "TV", 12, 34)], "path", 12, 34, "TV"));
    }

    [Fact]
    public void Refuses_ambiguous_fallback()
    {
        Assert.Throws<InvalidOperationException>(() => WindowsDisplayService.SelectTarget([Target(1, null, "TV", null, null), Target(2, null, "TV", null, null)], "path", 12, 34, "TV"));
    }
}

using System.Text.Json;
using CouchPad.Platform.Windows;
using CouchPad.Core.Display;

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

public class DisplayMappingTests
{
    private static WindowsDisplayService.Target Tv => new(1, 0, 10, "tv", "TV", 1, 2, 0);
    private static byte[] Path(uint source, uint target)
    {
        var data = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(data, 0);
        BitConverter.GetBytes(source).CopyTo(data, 8);
        BitConverter.GetBytes(1u).CopyTo(data, 20);
        BitConverter.GetBytes(target).CopyTo(data, 28);
        return data;
    }
    private static WindowsDisplayService.Snapshot Snapshot(params byte[][] paths) => new(1, 2, 72, 64, Convert.ToBase64String(paths.SelectMany(x => x).ToArray()), "", []);

    [Theory]
    [InlineData(0, DisplayMode.InternalOnly, true, false)]
    [InlineData(1, DisplayMode.TvOnly, false, true)]
    [InlineData(2, DisplayMode.Extend, true, true)]
    [InlineData(3, DisplayMode.Mirror, true, true)]
    public void Maps_active_paths(int scenario, CouchPad.Core.Display.DisplayMode expected, bool primary, bool tv)
    {
        var paths = scenario switch { 0 => new[] { Path(0, 20) }, 1 => new[] { Path(0, 10) }, 2 => new[] { Path(0, 20), Path(1, 10) }, _ => new[] { Path(0, 20), Path(0, 10) } };
        var result = WindowsDisplayService.MapState(Snapshot(paths), Tv);
        Assert.Equal(expected, result.Mode);
        Assert.Equal(primary, result.PrimaryActive);
        Assert.Equal(tv, result.TvActive);
        Assert.NotNull(result.Topology);
    }

    [Fact]
    public void Refuses_more_than_one_non_tv_target() => Assert.Throws<InvalidOperationException>(() => WindowsDisplayService.MapState(Snapshot(Path(0, 20), Path(1, 30)), Tv));

    [Fact]
    public void Writes_are_disabled_by_default()
    {
        var service = new WindowsDisplayService(new("tv", 1, 2));
        Assert.Throws<NotSupportedException>(() => { _ = service.ApplyAsync(CouchPad.Core.Display.DisplayMode.TvOnly); });
    }
}



public class DisplayTopologyPlannerTests
{
    private static byte[] Path(uint source, uint target, bool available = true)
    {
        var bytes = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(bytes, 0);
        BitConverter.GetBytes(source).CopyTo(bytes, 8);
        BitConverter.GetBytes(1u).CopyTo(bytes, 20);
        BitConverter.GetBytes(target).CopyTo(bytes, 28);
        BitConverter.GetBytes(available ? 1u : 0u).CopyTo(bytes, 60);
        return bytes;
    }
    private static WindowsDisplayService.Snapshot Snapshot(uint flags, params byte[][] paths) => new(1, flags, 72, 64, Convert.ToBase64String(paths.SelectMany(x => x).ToArray()), "", []);
    private static WindowsDisplayService.Target Tv => new(1, 0, 10, "tv", "TV", 1, 2, 0);

    [Theory]
    [InlineData(DisplayMode.TvOnly, 1, 0)]
    [InlineData(DisplayMode.Mirror, 2, 0)]
    [InlineData(DisplayMode.Extend, 2, 1)]
    public void Builds_proposal_for_inactive_tv(DisplayMode mode, int count, uint tvSource)
    {
        var baseline = Snapshot(2, Path(0, 20));
        var all = Snapshot(1, Path(0, 20), Path(0, 10), Path(1, 10));
        var result = DisplayTopologyPlanner.Create(baseline, all, Tv, mode);
        Assert.Equal(72 * count, result.Paths.Length);
        var last = (count - 1) * 72;
        Assert.Equal(tvSource, BitConverter.ToUInt32(result.Paths, last + 8));
        Assert.Equal(10u, BitConverter.ToUInt32(result.Paths, last + 28));
        Assert.Equal(uint.MaxValue, BitConverter.ToUInt32(result.Paths, last + 12));
        Assert.Equal(uint.MaxValue, BitConverter.ToUInt32(result.Paths, last + 32));
        Assert.Equal(1u, BitConverter.ToUInt32(result.Paths, last + 68) & 1);
        Assert.Equal(0u, BitConverter.ToUInt32(Convert.FromBase64String(all.PathsBase64), 72 + 68));
    }

    [Fact]
    public void Rejects_unavailable_target() => Assert.Throws<InvalidOperationException>(() => DisplayTopologyPlanner.Create(Snapshot(2, Path(0, 20)), Snapshot(1, Path(0, 10, false)), Tv, DisplayMode.Mirror));

    [Fact]
    public void Rejects_missing_distinct_source_for_extend() => Assert.Throws<InvalidOperationException>(() => DisplayTopologyPlanner.Create(Snapshot(2, Path(0, 20)), Snapshot(1, Path(0, 10)), Tv, DisplayMode.Extend));
}

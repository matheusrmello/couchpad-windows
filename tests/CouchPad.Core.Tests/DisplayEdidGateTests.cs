using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class DisplayEdidGateTests
{
    private static readonly byte[] EdidHeader = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
    private static readonly DisplayPreference Uhd60 = new(3840, 2160, 60, null);
    private const int DispChangeSuccessful = 0;
    private const int DispChangeBadMode = -2;

    [Fact]
    public async Task Unadvertised_mode_refused_by_the_sink_fails_as_unsupported_after_rollback()
    {
        var harness = new Harness();
        var service = harness.Create(EdidWithoutUhd60, _ => DispChangeBadMode);

        var error = await Assert.ThrowsAsync<DisplayApplyException>(() =>
            service.ApplyAsync(DisplayMode.TvOnly, Uhd60));

        Assert.Equal(DisplayApplyErrorCode.UnsupportedMode, error.Code);
        Assert.Equal(new DisplayModeOption(3840, 2160, 60), error.RequestedMode);
        Assert.Equal(DispChangeBadMode, error.NativeResult);
        Assert.False(DisplayApplyFailure.IsUnchanged(error));
        Assert.Equal(new uint[] { 0x40, 0x80, 0x40, 0x80 }, harness.Actions);
        Assert.Equal(DisplayMode.InternalOnly, service.GetCurrent().Mode);
    }

    [Fact]
    public async Task Advertised_mode_refused_by_the_sink_fails_as_rejected()
    {
        var harness = new Harness();
        var service = harness.Create(EdidWithUhd60, _ => DispChangeBadMode);

        var error = await Assert.ThrowsAsync<DisplayApplyException>(() =>
            service.ApplyAsync(DisplayMode.TvOnly, Uhd60));

        Assert.Equal(DisplayApplyErrorCode.ModeRejected, error.Code);
        Assert.Equal(new DisplayModeOption(3840, 2160, 60), error.RequestedMode);
        Assert.Equal(new uint[] { 0x40, 0x80, 0x40, 0x80 }, harness.Actions);
        Assert.Equal(DisplayMode.InternalOnly, service.GetCurrent().Mode);
    }

    [Fact]
    public async Task Unadvertised_mode_accepted_by_the_sink_is_not_vetoed_by_the_edid()
    {
        var harness = new Harness();
        var service = harness.Create(EdidWithoutUhd60, _ => DispChangeSuccessful);

        var applied = await service.ApplyAsync(DisplayMode.TvOnly, Uhd60);

        Assert.Equal(DisplayMode.TvOnly, applied.Mode);
        Assert.Equal(new uint[] { 0x40, 0x80 }, harness.Actions);
    }

    [Fact]
    public void Gate_opens_without_edid_and_matches_refresh_within_one_hertz()
    {
        Assert.True(WindowsDisplayService.IsModeAdvertised(new(3840, 2160, 60), []));
        Assert.True(WindowsDisplayService.IsModeAdvertised(new(3840, 2160, 60), [new(3840, 2160, 60)]));
        Assert.True(WindowsDisplayService.IsModeAdvertised(new(3840, 2160, 29), [new(3840, 2160, 30)]));
        Assert.True(WindowsDisplayService.IsModeAdvertised(new(3840, 2160, 31), [new(3840, 2160, 30)]));
        Assert.False(WindowsDisplayService.IsModeAdvertised(new(3840, 2160, 60), [new(3840, 2160, 30)]));
        Assert.False(WindowsDisplayService.IsModeAdvertised(new(2560, 1440, 60), [new(3840, 2160, 60)]));
    }

    private static byte[]? EdidWithoutUhd60(string _) => EdidWithVideoId(95);

    private static byte[]? EdidWithUhd60(string _) => EdidWithVideoId(97);

    private sealed class Harness
    {
        private WindowsDisplayService.Snapshot _active = Snapshot(2, Path(0, 20));

        public List<uint> Actions { get; } = [];

        public WindowsDisplayService Create(Func<string, byte[]?> edid, Func<DisplayModeOption, int> changeMode) =>
            new(new WindowsDisplayOptions("tv", 1, 2, AllowWrites: true, InternalDisplay: new("internal", 3, 4)),
                flags => flags == WindowsDisplayService.AllPaths ? All() : _active,
                (paths, _, action) =>
                {
                    Actions.Add(action);
                    if (action == 0x80) _active = Snapshot(2, SplitPaths(paths));
                    return 0;
                },
                edid,
                changeMode);
    }

    private static WindowsDisplayService.Snapshot All() => Snapshot(1, Path(0, 20), Path(0, 10), Path(1, 10));

    private static WindowsDisplayService.Snapshot Snapshot(uint flags, params byte[][] paths) => new(
        1, flags, 72, 64, Convert.ToBase64String(paths.SelectMany(path => path).ToArray()), Convert.ToBase64String(Modes()),
        [new(1, 0, 10, "tv", "TV", 1, 2, 0), new(1, 0, 20, "internal", "Internal", 3, 4, 0)]);

    private static byte[][] SplitPaths(byte[] paths) =>
        Enumerable.Range(0, paths.Length / 72).Select(index => paths.AsSpan(index * 72, 72).ToArray()).ToArray();

    private static byte[] Path(uint source, uint target)
    {
        var path = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(path, 0);
        BitConverter.GetBytes(source).CopyTo(path, 8);
        BitConverter.GetBytes(source).CopyTo(path, 12);
        BitConverter.GetBytes(1u).CopyTo(path, 20);
        BitConverter.GetBytes(target).CopyTo(path, 28);
        BitConverter.GetBytes(target == 10 ? 2u : 3u).CopyTo(path, 32);
        BitConverter.GetBytes(1u).CopyTo(path, 60);
        BitConverter.GetBytes(1u).CopyTo(path, 68);
        return path;
    }

    /// <summary>
    /// Four mode blocks so the snapshot comparer can resolve every source and
    /// target used by the fixtures: source 0, source 1, target 10 (TV) and
    /// target 20 (internal).
    /// </summary>
    private static byte[] Modes()
    {
        var modes = new byte[4 * 64];
        WriteMode(modes, 0, type: 1, id: 0);
        WriteMode(modes, 1, type: 1, id: 1);
        WriteMode(modes, 2, type: 2, id: 10);
        WriteMode(modes, 3, type: 2, id: 20);
        return modes;
    }

    private static void WriteMode(byte[] modes, int index, uint type, uint id)
    {
        var offset = index * 64;
        BitConverter.GetBytes(type).CopyTo(modes, offset);
        BitConverter.GetBytes(id).CopyTo(modes, offset + 4);
        BitConverter.GetBytes(1u).CopyTo(modes, offset + 8);
    }

    private static byte[] EdidWithVideoId(int videoId)
    {
        var edid = new byte[256];
        EdidHeader.CopyTo(edid, 0);
        edid[126] = 1;
        edid[128] = 0x02;
        edid[129] = 0x03;
        edid[130] = 6;
        edid[132] = 0x41;
        edid[133] = (byte)videoId;
        return edid;
    }
}

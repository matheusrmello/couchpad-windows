using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class DisplayEdidGateTests
{
    private static readonly byte[] EdidHeader = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    [Fact]
    public async Task Edid_gate_refuses_a_mode_the_sink_does_not_advertise_before_any_write()
    {
        var actions = new List<uint>();
        var service = CreateService(actions, _ => EdidWithVideoId(95));

        var error = await Assert.ThrowsAsync<DisplayApplyException>(() =>
            service.ApplyAsync(DisplayMode.TvOnly, new DisplayPreference(3840, 2160, 60, null)));

        Assert.Equal(DisplayApplyErrorCode.UnsupportedMode, error.Code);
        Assert.Equal(new DisplayModeOption(3840, 2160, 60), error.RequestedMode);
        Assert.True(DisplayApplyFailure.IsUnchanged(error));
        Assert.Empty(actions);
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

    private static WindowsDisplayService CreateService(List<uint> actions, Func<string, byte[]?> edid) =>
        new(
            new WindowsDisplayOptions("tv", 1, 2, AllowWrites: true, InternalDisplay: new("internal", 3, 4)),
            flags => flags == WindowsDisplayService.AllPaths ? All() : Active(),
            (paths, modes, action) => { actions.Add(action); return 0; },
            edid);

    private static WindowsDisplayService.Snapshot Active() => Snapshot(2, Path(0, 20));

    private static WindowsDisplayService.Snapshot All() => Snapshot(1, Path(0, 20), Path(0, 10), Path(1, 10));

    private static byte[] Path(uint source, uint target)
    {
        var path = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(path, 0);
        BitConverter.GetBytes(source).CopyTo(path, 8);
        BitConverter.GetBytes(1u).CopyTo(path, 20);
        BitConverter.GetBytes(target).CopyTo(path, 28);
        BitConverter.GetBytes(1u).CopyTo(path, 60);
        BitConverter.GetBytes(1u).CopyTo(path, 68);
        return path;
    }

    private static WindowsDisplayService.Snapshot Snapshot(uint flags, params byte[][] paths) => new(
        1, flags, 72, 64, Convert.ToBase64String(paths.SelectMany(path => path).ToArray()), Convert.ToBase64String(new byte[64]),
        [new(1, 0, 10, "tv", "TV", 1, 2, 0), new(1, 0, 20, "internal", "Internal", 3, 4, 0)]);

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

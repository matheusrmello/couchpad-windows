using System.Buffers.Binary;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class WindowsDisplayModesTests
{
    private static readonly byte[] EdidHeader = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
    private const string TvDevicePath = @"\\?\DISPLAY#GSM5B7B#5&1a2b3c4d&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    [Fact]
    public void Available_modes_come_from_detailed_timing_and_cea_video_block()
    {
        var service = CreateService(WithCeaVideoBlock(CreateEdid((29700, 3840, 560, 2160, 90)), 16));

        var modes = service.GetAvailableModes();

        Assert.Equal(
            [new DisplayModeOption(3840, 2160, 30), new DisplayModeOption(1920, 1080, 60)],
            modes);
    }

    [Fact]
    public void Missing_edid_yields_no_available_modes()
    {
        var service = CreateService(null);

        Assert.Empty(service.GetAvailableModes());
    }

    [Fact]
    public void Edid_modes_are_loaded_once_and_cached()
    {
        var reads = 0;
        var service = new WindowsDisplayService(
            CreateOptions(),
            devicePath =>
            {
                Assert.Equal(TvDevicePath, devicePath);
                reads++;
                return WithCeaVideoBlock(CreateEdid((29700, 3840, 560, 2160, 90)), 16);
            });

        var first = service.GetAvailableModes();
        var second = service.GetAvailableModes();

        Assert.Equal(first, second);
        Assert.Equal(1, reads);
        Assert.Equal([new DisplayModeOption(3840, 2160, 30), new DisplayModeOption(1920, 1080, 60)], second);
    }

    [Fact]
    public void Merge_modes_adds_windows_modes_and_deduplicates_by_resolution_and_refresh()
    {
        var edid = new[] { new DisplayModeOption(3840, 2160, 30), new DisplayModeOption(1920, 1080, 60) };
        var windows = new[]
        {
            new DisplayModeOption(3840, 2160, 60),
            new DisplayModeOption(1920, 1080, 60),
            new DisplayModeOption(3840, 2160, 30),
        };

        var merged = WindowsDisplayService.MergeModes(edid, windows);

        Assert.Equal(
            [
                new DisplayModeOption(3840, 2160, 60),
                new DisplayModeOption(3840, 2160, 30),
                new DisplayModeOption(1920, 1080, 60),
            ],
            merged);
    }

    [Fact]
    public void Available_modes_fall_back_to_edid_when_the_tv_has_no_gdi_device()
    {
        var service = CreateService(WithCeaVideoBlock(CreateEdid((29700, 3840, 560, 2160, 90)), 16));

        Assert.Equal(
            [new DisplayModeOption(3840, 2160, 30), new DisplayModeOption(1920, 1080, 60)],
            service.GetAvailableModes());
    }

    [Fact]
    public void Native_struct_sizes_match_windows_abi()
    {
        Assert.Equal(220, WindowsDisplayService.DevModeSizeBytes);
        Assert.Equal(840, WindowsDisplayService.DisplayDeviceSizeBytes);
    }

    private static WindowsDisplayService CreateService(byte[]? edid) =>
        new(CreateOptions(), _ => edid);

    private static WindowsDisplayOptions CreateOptions() => new(TvDevicePath, 0x4C2D, 0x0B7B, ExactName: "CouchPad TV");

    private static byte[] CreateEdid(params (ushort PixelClock, int HActive, int HBlank, int VActive, int VBlank)[] timings)
    {
        var edid = new byte[128];
        EdidHeader.CopyTo(edid, 0);

        for (var index = 0; index < timings.Length && index < 4; index++)
            WriteDetailedTiming(edid.AsSpan(54 + (index * 18), 18), timings[index]);

        return edid;
    }

    private static void WriteDetailedTiming(
        Span<byte> dtd,
        (ushort PixelClock, int HActive, int HBlank, int VActive, int VBlank) timing)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(dtd, timing.PixelClock);
        dtd[2] = (byte)(timing.HActive & 0xFF);
        dtd[3] = (byte)(timing.HBlank & 0xFF);
        dtd[4] = (byte)(((timing.HActive >> 8) << 4) | ((timing.HBlank >> 8) & 0x0F));
        dtd[5] = (byte)(timing.VActive & 0xFF);
        dtd[6] = (byte)(timing.VBlank & 0xFF);
        dtd[7] = (byte)(((timing.VActive >> 8) << 4) | ((timing.VBlank >> 8) & 0x0F));
    }

    private static byte[] WithCeaVideoBlock(byte[] baseEdid, params int[] videoIds)
    {
        var extension = new byte[128];
        extension[0] = 0x02;
        extension[1] = 0x03;
        extension[4] = (byte)(0x40 | videoIds.Length);
        for (var index = 0; index < videoIds.Length; index++)
            extension[5 + index] = (byte)videoIds[index];
        extension[2] = (byte)(5 + videoIds.Length);

        var edid = new byte[baseEdid.Length + extension.Length];
        baseEdid.CopyTo(edid, 0);
        extension.CopyTo(edid, baseEdid.Length);
        edid[126] = 1;

        return edid;
    }
}

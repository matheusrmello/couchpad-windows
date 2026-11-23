using System.Buffers.Binary;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class EdidModeParserTests
{
    private static readonly byte[] EdidHeader = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    [Fact]
    public void Detailed_timing_and_cea_video_block_are_both_reported()
    {
        var edid = WithCeaVideoBlock(CreateEdid((29700, 3840, 560, 2160, 90)), 16);

        var modes = EdidModeParser.Parse(edid);

        Assert.Equal(
            [new DisplayModeOption(3840, 2160, 30), new DisplayModeOption(1920, 1080, 60)],
            modes);
    }

    [Fact]
    public void Invalid_header_or_short_input_yields_empty()
    {
        Assert.Empty(EdidModeParser.Parse(ReadOnlySpan<byte>.Empty));
        Assert.Empty(EdidModeParser.Parse(new byte[100]));

        var badHeader = CreateEdid((29700, 3840, 560, 2160, 90));
        badHeader[7] = 0x01;

        Assert.Empty(EdidModeParser.Parse(badHeader));
    }

    [Fact]
    public void Detailed_timing_with_zero_pixel_clock_is_ignored()
    {
        var edid = CreateEdid((0, 1920, 280, 1080, 45));

        Assert.Empty(EdidModeParser.Parse(edid));
    }

    [Fact]
    public void Interlaced_video_ids_are_ignored()
    {
        var edid = WithCeaVideoBlock(CreateEdid(), 5, 6, 7, 20, 21, 22);

        Assert.Empty(EdidModeParser.Parse(edid));
    }

    [Fact]
    public void Duplicate_detailed_timing_and_vic_yield_a_single_mode()
    {
        var edid = WithCeaVideoBlock(CreateEdid((14850, 1920, 280, 1080, 45)), 16);

        var modes = EdidModeParser.Parse(edid);

        var mode = Assert.Single(modes);
        Assert.Equal(new DisplayModeOption(1920, 1080, 60), mode);
    }

    [Fact]
    public void Modes_are_sorted_by_area_then_refresh_descending()
    {
        var edid = WithCeaVideoBlock(CreateEdid((29700, 3840, 560, 2160, 90)), 4, 16);

        var modes = EdidModeParser.Parse(edid);

        Assert.Equal(
            [
                new DisplayModeOption(3840, 2160, 30),
                new DisplayModeOption(1920, 1080, 60),
                new DisplayModeOption(1280, 720, 60),
            ],
            modes);
    }

    [Fact]
    public void Truncated_extension_block_is_ignored()
    {
        var baseEdid = CreateEdid((29700, 3840, 560, 2160, 90));
        var truncated = new byte[baseEdid.Length + 100];
        baseEdid.CopyTo(truncated, 0);
        truncated[126] = 1;
        truncated[128] = 0x02;
        truncated[130] = 8;

        var mode = Assert.Single(EdidModeParser.Parse(truncated));
        Assert.Equal(new DisplayModeOption(3840, 2160, 30), mode);
    }

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

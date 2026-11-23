using System.Buffers.Binary;
using CouchPad.Core.Display;

namespace CouchPad.Platform.Windows;

/// <summary>
/// Enumerates the display modes declared by an EDID block: detailed timing
/// descriptors from the base block plus CEA-861 video data blocks. Pure code —
/// no Win32 calls, no display writes, and invalid input yields an empty list.
/// </summary>
public static class EdidModeParser
{
    private const int BlockSize = 128;
    private const int BaseDtdCount = 4;
    private const int DtdSize = 18;
    private const int FirstDtdOffset = 54;
    private const int CeaExtensionTag = 0x02;
    private const int VideoDataBlockTag = 0x02;
    private const int FirstDataBlockOffset = 4;
    private const int MaxRefreshHz = 1000;

    private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    /// <summary>Common progressive CEA-861 video identification codes; interlaced VICs are absent on purpose.</summary>
    private static readonly IReadOnlyDictionary<int, DisplayModeOption> VideoIds =
        new Dictionary<int, DisplayModeOption>
        {
            [4] = new(1280, 720, 60),
            [16] = new(1920, 1080, 60),
            [19] = new(1280, 720, 50),
            [31] = new(1920, 1080, 50),
            [32] = new(1920, 1080, 24),
            [33] = new(1920, 1080, 25),
            [34] = new(1920, 1080, 30),
            [93] = new(3840, 2160, 24),
            [94] = new(3840, 2160, 25),
            [95] = new(3840, 2160, 30),
            [96] = new(3840, 2160, 50),
            [97] = new(3840, 2160, 60),
        };

    /// <summary>
    /// Returns the distinct modes found in <paramref name="edid"/>, sorted by pixel
    /// area descending and then refresh descending. Never throws.
    /// </summary>
    public static IReadOnlyList<DisplayModeOption> Parse(ReadOnlySpan<byte> edid)
    {
        try
        {
            if (edid.Length < BlockSize || !edid[..Header.Length].SequenceEqual(Header))
                return Array.Empty<DisplayModeOption>();

            var modes = new List<DisplayModeOption>();
            var seen = new HashSet<(int Width, int Height, int RefreshHz)>();

            for (var index = 0; index < BaseDtdCount; index++)
                AddDetailedTiming(edid.Slice(FirstDtdOffset + (index * DtdSize), DtdSize), modes, seen);

            for (var offset = BlockSize; offset + BlockSize <= edid.Length; offset += BlockSize)
                AddCeaVideoModes(edid.Slice(offset, BlockSize), modes, seen);

            return modes
                .OrderByDescending(mode => (long)mode.Width * mode.Height)
                .ThenByDescending(mode => mode.RefreshHz)
                .ThenByDescending(mode => mode.Width)
                .ToArray();
        }
        catch
        {
            return Array.Empty<DisplayModeOption>();
        }
    }

    private static void AddDetailedTiming(
        ReadOnlySpan<byte> dtd,
        List<DisplayModeOption> modes,
        HashSet<(int Width, int Height, int RefreshHz)> seen)
    {
        var pixelClock = BinaryPrimitives.ReadUInt16LittleEndian(dtd);
        if (pixelClock == 0)
            return;

        var hActive = dtd[2] | ((dtd[4] & 0xF0) << 4);
        var hBlank = dtd[3] | ((dtd[4] & 0x0F) << 8);
        var vActive = dtd[5] | ((dtd[7] & 0xF0) << 4);
        var vBlank = dtd[6] | ((dtd[7] & 0x0F) << 8);

        var hTotal = hActive + hBlank;
        var vTotal = vActive + vBlank;
        if (hActive <= 0 || vActive <= 0 || hTotal <= 0 || vTotal <= 0)
            return;

        var pixelClockHz = pixelClock * 10_000L;
        var refresh = (int)Math.Round(
            (double)pixelClockHz / ((long)hTotal * vTotal),
            MidpointRounding.AwayFromZero);
        if (refresh is <= 0 or > MaxRefreshHz)
            return;

        Add(new DisplayModeOption(hActive, vActive, refresh), modes, seen);
    }

    private static void AddCeaVideoModes(
        ReadOnlySpan<byte> block,
        List<DisplayModeOption> modes,
        HashSet<(int Width, int Height, int RefreshHz)> seen)
    {
        if (block[0] != CeaExtensionTag)
            return;

        var dtdOffset = block[2];
        if (dtdOffset < FirstDataBlockOffset)
            return;

        var position = FirstDataBlockOffset;
        while (position < dtdOffset && position < block.Length)
        {
            var header = block[position];
            var length = header & 0x1F;
            if (length == 0)
                break;

            var payloadStart = position + 1;
            if (payloadStart + length > block.Length || payloadStart + length > dtdOffset)
                break;

            if ((header >> 5) == VideoDataBlockTag)
            {
                for (var index = payloadStart; index < payloadStart + length; index++)
                {
                    var videoId = block[index] & 0x7F;
                    if (VideoIds.TryGetValue(videoId, out var mode))
                        Add(mode, modes, seen);
                }
            }

            position = payloadStart + length;
        }
    }

    private static void Add(
        DisplayModeOption mode,
        List<DisplayModeOption> modes,
        HashSet<(int Width, int Height, int RefreshHz)> seen)
    {
        if (mode.Width <= 0 || mode.Height <= 0 || mode.RefreshHz <= 0)
            return;

        if (seen.Add((mode.Width, mode.Height, mode.RefreshHz)))
            modes.Add(mode);
    }
}

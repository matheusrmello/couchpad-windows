using System.Buffers.Binary;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class RtssSharedMemoryParserTests
{
    // Layout sintético usado nos testes: cabeçalho de 20 bytes, preenchimento até
    // 64 e uma única entrada de app de 288 bytes a partir do offset 64.
    private const int SyntheticHeaderLength = 64;
    private const int SyntheticEntrySize = 288;

    [Fact]
    public void FindApp_reads_counters_for_matching_pid()
    {
        var view = CreateView(appArrSize: 1);
        WriteApp(view, SyntheticHeaderLength, pid: 1234, frames: 600, time0: 0, time1: 10000);

        var counters = RtssSharedMemoryParser.FindApp(view, 1234);

        Assert.NotNull(counters);
        Assert.Equal(new RtssAppCounters(600, 0, 10000), counters!.Value);
        var fps = RtssSharedMemoryParser.AverageFramesPerSecond(
            counters.Value.Frames, counters.Value.Time0, counters.Value.Time1);
        Assert.Equal(60d, fps!.Value, precision: 6);
    }

    [Fact]
    public void FindApp_rejects_wrong_signature()
    {
        var view = CreateView(appArrSize: 1, signature: 0x52545354);
        WriteApp(view, SyntheticHeaderLength, pid: 1234, frames: 600, time0: 0, time1: 10000);

        Assert.Null(RtssSharedMemoryParser.FindApp(view, 1234));
    }

    [Fact]
    public void FindApp_rejects_view_shorter_than_header()
    {
        var view = new byte[RtssSharedMemoryParser.HeaderSize - 1];
        BinaryPrimitives.WriteUInt32LittleEndian(view, RtssSharedMemoryParser.Signature);

        Assert.Null(RtssSharedMemoryParser.FindApp(view, 1234));
    }

    [Fact]
    public void FindApp_rejects_entry_size_below_minimum()
    {
        var view = CreateView(appEntrySize: RtssSharedMemoryParser.MinAppEntrySize - 1, appArrSize: 1);
        WriteApp(view, SyntheticHeaderLength, pid: 1234, frames: 600, time0: 0, time1: 10000);

        Assert.Null(RtssSharedMemoryParser.FindApp(view, 1234));
    }

    [Fact]
    public void FindApp_rejects_absurd_array_size_without_throwing()
    {
        var view = CreateView(appArrSize: 10000, length: 512);

        Assert.Null(RtssSharedMemoryParser.FindApp(view, 1234));
    }

    [Fact]
    public void FindApp_rejects_array_offset_inside_header()
    {
        var view = CreateView(appArrOffset: 12, appArrSize: 1, length: 1024);

        Assert.Null(RtssSharedMemoryParser.FindApp(view, 1234));
    }

    [Fact]
    public void FindApp_finds_app_array_beyond_512_kib_prefix()
    {
        // Regressão: no RTSS 2.x real o array começa em ~2,3 MiB, muito além do
        // antigo teto de 512 KiB do buffer copiado. O parser precisa achar o app
        // quando a cópia cobre a região inteira (5,5 MiB).
        const uint appArrOffset = 2_396_256;
        const uint appArrSize = 256;
        var view = new byte[5_574_752];

        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(0, 4), RtssSharedMemoryParser.Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(4, 4), 0x00020015); // version real
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(8, 4), SyntheticEntrySize);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(12, 4), appArrOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(16, 4), appArrSize);

        WriteApp(view, (int)appArrOffset, pid: 1234, frames: 600, time0: 0, time1: 10000);

        var counters = RtssSharedMemoryParser.FindApp(view, 1234);

        Assert.NotNull(counters);
        Assert.Equal(new RtssAppCounters(600, 0, 10000), counters!.Value);
        var fps = RtssSharedMemoryParser.AverageFramesPerSecond(
            counters.Value.Frames, counters.Value.Time0, counters.Value.Time1);
        Assert.Equal(60d, fps!.Value, precision: 6);
    }

    [Fact]
    public void FindApp_returns_null_when_pid_is_missing()
    {
        var view = CreateView(appArrSize: 2);
        WriteApp(view, SyntheticHeaderLength, pid: 4321, frames: 300, time0: 0, time1: 5000);
        WriteApp(view, SyntheticHeaderLength + SyntheticEntrySize, pid: 8765, frames: 400, time0: 0, time1: 8000);

        Assert.Null(RtssSharedMemoryParser.FindApp(view, 1234));
    }

    [Fact]
    public void FindApp_ignores_entries_after_the_matching_pid()
    {
        var view = CreateView(appArrSize: 2);
        WriteApp(view, SyntheticHeaderLength + SyntheticEntrySize, pid: 99, frames: 999, time0: 0, time1: 1000);
        WriteApp(view, SyntheticHeaderLength, pid: 1234, frames: 60, time0: 1000, time1: 2000);

        var counters = RtssSharedMemoryParser.FindApp(view, 1234);

        Assert.Equal(new RtssAppCounters(60, 1000, 2000), counters);
    }

    [Theory]
    [InlineData(0u, 0u, 10000u)]
    [InlineData(600u, 5000u, 5000u)]
    [InlineData(600u, 6000u, 5000u)]
    public void AverageFramesPerSecond_rejects_empty_or_invalid_window(uint frames, uint time0, uint time1)
    {
        Assert.Null(RtssSharedMemoryParser.AverageFramesPerSecond(frames, time0, time1));
    }

    [Fact]
    public void AverageFramesPerSecond_scales_millisecond_window()
    {
        Assert.Equal(30d, RtssSharedMemoryParser.AverageFramesPerSecond(300, 1000, 11000)!.Value, precision: 6);
    }

    [Fact]
    public void AverageFramesPerSecond_handles_short_one_second_window()
    {
        // Janela típica do RTSS (~1 s): o contador reinicia a cada janela. Um
        // caminho de delta entre amostras produziria ~1 FPS aqui.
        Assert.Equal(60d, RtssSharedMemoryParser.AverageFramesPerSecond(60, 1000, 2000)!.Value, precision: 6);
        Assert.Equal(58.25d, RtssSharedMemoryParser.AverageFramesPerSecond(233, 5000, 9000)!.Value, precision: 6);
    }

    private static byte[] CreateView(
        uint signature = RtssSharedMemoryParser.Signature,
        uint appEntrySize = SyntheticEntrySize,
        uint appArrOffset = SyntheticHeaderLength,
        uint appArrSize = 1,
        int? length = null)
    {
        var view = new byte[length ?? SyntheticHeaderLength + SyntheticEntrySize * 2];
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(0, 4), signature);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(4, 4), 2); // version
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(8, 4), appEntrySize);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(12, 4), appArrOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(16, 4), appArrSize);
        return view;
    }

    private static void WriteApp(byte[] view, int offset, uint pid, uint frames, uint time0, uint time1)
    {
        // Layout real do RTSS 2.x: pid=+0, time0=+268, time1=+272, frames=+276.
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(offset, 4), pid);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(offset + 268, 4), time0);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(offset + 272, 4), time1);
        BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan(offset + 276, 4), frames);
    }
}

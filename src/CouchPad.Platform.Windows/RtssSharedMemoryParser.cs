using System.Buffers.Binary;

namespace CouchPad.Platform.Windows;

/// <summary>Parser read-only do bloco RTSSSharedMemoryV2 (RTSS).</summary>
/// <remarks>
/// Opera somente sobre bytes já mapeados: não abre mapeamento, não faz I/O e não
/// depende de estado global. A leitura real da memória compartilhada fica a cargo
/// da fonte Windows.
/// </remarks>
public static class RtssSharedMemoryParser
{
    /// <summary>Assinatura 'RTSS' em little-endian.</summary>
    public const uint Signature = 0x52545353; // 'RTSS'

    /// <summary>Tamanho mínimo do cabeçalho lido (até <c>dwAppArrSize</c>).</summary>
    public const int HeaderSize = 20;

    /// <summary>Tamanho mínimo aceito para uma entrada de app do RTSS 2.x.</summary>
    public const int MinAppEntrySize = 288;

    // Cabeçalho do bloco RTSS_SHARED_MEMORY:
    // signature=0, version=4, appEntrySize=8, appArrOffset=12, appArrSize=16.
    private const int AppEntrySizeOffset = 8;
    private const int AppArrOffsetOffset = 12;
    private const int AppArrSizeOffset = 16;

    // Prefixo estável da entrada de app:
    // pid=0, flags=4, name=8..267 (260), renderApi=268, frames=272, time0=276, time1=280.
    private const int PidOffset = 0;
    private const int FramesOffset = 272;
    private const int Time0Offset = 276;
    private const int Time1Offset = 280;

    /// <summary>
    /// Localiza a entrada do processo informado e devolve seus contadores de frame.
    /// </summary>
    /// <param name="view">Visão somente leitura do bloco mapeado.</param>
    /// <param name="processId">PID do app procurado.</param>
    /// <returns>Contadores do app ou <c>null</c> quando a visão é inválida ou o PID não existe.</returns>
    public static RtssAppCounters? FindApp(ReadOnlySpan<byte> view, uint processId)
    {
        if (view.Length < HeaderSize)
            return null;

        if (BinaryPrimitives.ReadUInt32LittleEndian(view) != Signature)
            return null;

        var appEntrySize = BinaryPrimitives.ReadUInt32LittleEndian(view.Slice(AppEntrySizeOffset, 4));
        var appArrOffset = BinaryPrimitives.ReadUInt32LittleEndian(view.Slice(AppArrOffsetOffset, 4));
        var appArrSize = BinaryPrimitives.ReadUInt32LittleEndian(view.Slice(AppArrSizeOffset, 4));

        // Validação fail-closed: qualquer campo implausível invalida o bloco inteiro.
        if (appEntrySize < MinAppEntrySize)
            return null;
        if (appArrOffset < HeaderSize)
            return null;
        if (appArrSize > 256)
            return null;

        // Multiplicação em long para não estourar uint/int com valores hostis.
        var entriesEnd = appArrOffset + (long)appEntrySize * appArrSize;
        if (entriesEnd > view.Length)
            return null;

        for (var index = 0; index < (int)appArrSize; index++)
        {
            var offset = (int)(appArrOffset + (long)appEntrySize * index);
            var entry = view.Slice(offset);
            if (BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(PidOffset, 4)) != processId)
                continue;

            var frames = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(FramesOffset, 4));
            var time0 = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(Time0Offset, 4));
            var time1 = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(Time1Offset, 4));
            return new RtssAppCounters(frames, time0, time1);
        }

        return null;
    }

    /// <summary>
    /// Calcula o FPS médio a partir do contador de frames e do intervalo em milissegundos.
    /// </summary>
    /// <param name="frames">Frames acumulados no intervalo.</param>
    /// <param name="time0">Início da janela, em milissegundos.</param>
    /// <param name="time1">Fim da janela, em milissegundos.</param>
    /// <returns>FPS médio ou <c>null</c> quando não há frames ou o intervalo é inválido.</returns>
    public static double? AverageFramesPerSecond(uint frames, uint time0, uint time1)
    {
        if (frames == 0)
            return null;
        if (time1 <= time0)
            return null;

        return frames * 1000.0 / (time1 - time0);
    }

    /// <summary>
    /// Calcula o FPS instantâneo entre duas amostras do mesmo processo.
    /// </summary>
    /// <param name="frames">Frames acumulados na amostra atual.</param>
    /// <param name="lastFrames">Frames acumulados na amostra anterior.</param>
    /// <param name="time1">Fim da janela atual, em milissegundos.</param>
    /// <param name="lastTime1">Fim da janela anterior, em milissegundos.</param>
    /// <returns>FPS do intervalo ou <c>null</c> em wrap/reset ou intervalo sem frames.</returns>
    public static double? DeltaFramesPerSecond(uint frames, uint lastFrames, uint time1, uint lastTime1)
    {
        if (time1 < lastTime1 || frames < lastFrames)
            return null; // wrap/reset

        var deltaMs = time1 - lastTime1;
        var deltaFrames = frames - lastFrames;
        if (deltaMs == 0 || deltaFrames == 0)
            return null;
        return deltaFrames * 1000.0 / deltaMs;
    }
}

/// <summary>Contadores de frame lidos de um app no RTSS.</summary>
/// <param name="Frames">Frames acumulados na janela atual.</param>
/// <param name="Time0">Início da janela, em milissegundos.</param>
/// <param name="Time1">Fim da janela, em milissegundos.</param>
public readonly record struct RtssAppCounters(uint Frames, uint Time0, uint Time1);

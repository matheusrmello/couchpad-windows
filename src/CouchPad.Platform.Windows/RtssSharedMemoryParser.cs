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

    // Prefixo estável da entrada de app, conforme o layout real do RTSS 2.x
    // (confirmado em 0x00020015 com appEntrySize=12416): não existe dwFlags.
    //   +0   dwProcessID
    //   +4   szName[260]      (4..263)
    //   +264 dwRenderAPI
    //   +268 dwTime0
    //   +272 dwTime1
    //   +276 dwFrames
    //   +280 dwFrameTime      (microssegundos, não usado)
    // Só o prefixo até +280 é lido; o restante da entrada e o stride continuam
    // valendo por appEntrySize.
    private const int PidOffset = 0;
    private const int Time0Offset = 268;
    private const int Time1Offset = 272;
    private const int FramesOffset = 276;

    /// <summary>
    /// Localiza a entrada do processo informado e devolve seus contadores de frame.
    /// </summary>
    /// <remarks>
    /// O prefixo lido da entrada segue o layout real do RTSS 2.x:
    /// <c>dwProcessID</c> em +0, <c>dwRenderAPI</c> em +264, <c>dwTime0</c> em +268,
    /// <c>dwTime1</c> em +272 e <c>dwFrames</c> em +276. As entradas são percorridas
    /// com stride <c>dwAppEntrySize</c> a partir de <c>dwAppArrOffset</c>.
    /// </remarks>
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
    /// <remarks>
    /// O RTSS reinicia <c>dwFrames</c> a cada janela (~1 s), então o contador é
    /// por janela, nunca acumulado entre leituras. Esta é a única fórmula de FPS
    /// usada pela fonte: não existe caminho de delta entre amostras.
    /// </remarks>
    /// <param name="frames">Frames contados na janela (não acumulados entre leituras).</param>
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
}

/// <summary>Contadores de frame lidos de um app no RTSS.</summary>
/// <param name="Frames">Frames contados na janela atual (o RTSS zera a cada ~1 s).</param>
/// <param name="Time0">Início da janela, em milissegundos.</param>
/// <param name="Time1">Fim da janela, em milissegundos.</param>
public readonly record struct RtssAppCounters(uint Frames, uint Time0, uint Time1);

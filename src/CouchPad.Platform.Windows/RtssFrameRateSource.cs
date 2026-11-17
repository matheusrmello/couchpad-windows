using System.Runtime.InteropServices;
using CouchPad.Core.Performance;

namespace CouchPad.Platform.Windows;

/// <summary>Fonte de FPS por processo via memória compartilhada do RTSS.</summary>
/// <remarks>
/// Somente leitura: abre o bloco <c>RTSSSharedMemoryV2</c>, copia a região
/// mapeada inteira (limitada a 16 MiB por sanidade) e fecha tudo na mesma
/// chamada. Não injeta nada em jogos, não escreve na memória do RTSS e não
/// exige privilégio de administrador. Quando o RTSS não está instalado, o
/// mapeamento global é negado ou o processo não tem entrada, a leitura
/// devolve <c>null</c>. A fonte é sem estado: cada chamada devolve a média da
/// janela corrente publicada pelo RTSS, sem comparar amostras anteriores.
/// </remarks>
public sealed class RtssFrameRateSource : IFrameRateSource
{
    private const uint FileMapRead = 0x0004;

    /// <summary>Teto de sanidade para descartar leituras corrompidas do bloco.</summary>
    private const double MaxFramesPerSecond = 999;

    /// <summary>
    /// Limite de bytes copiados do mapeamento. O array de apps fica em
    /// <c>dwAppArrOffset</c> (~2,3 MiB no RTSS 2.x real), então o teto precisa
    /// cobrir o bloco inteiro (região de ~5,5 MiB); 16 MiB só protege contra
    /// tamanhos absurdos reportados pelo sistema.
    /// </summary>
    private const ulong MaxViewBytes = 16 * 1024 * 1024;

    // O RTSS publica o bloco na sessão global para que outros usuários leiam.
    // A segunda tentativa cobre instalações que publicam apenas no namespace local.
    private static readonly string[] MappingNames = [@"Global\RTSSSharedMemoryV2", "RTSSSharedMemoryV2"];

    /// <inheritdoc />
    public double? ReadFramesPerSecond(int processId, DateTimeOffset now)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        if (processId <= 0)
            return null;

        try
        {
            var buffer = TryReadSharedMemory();
            if (buffer is null)
                return null;

            var counters = RtssSharedMemoryParser.FindApp(buffer, (uint)processId);
            if (counters is null)
                return null;

            // O RTSS reinicia dwFrames a cada janela (~1 s), então o contador é
            // POR JANELA e não acumulado: a única leitura correta é a média da
            // janela corrente (frames / (time1 - time0)). Derivar entre amostras
            // produz valores espúrios (ex.: ~1 FPS).
            var value = counters.Value;
            var framesPerSecond = RtssSharedMemoryParser.AverageFramesPerSecond(
                value.Frames, value.Time0, value.Time1);
            if (framesPerSecond is null)
                return null;
            var result = framesPerSecond.Value;
            if (!double.IsFinite(result) || result < 0)
                return null;
            return Math.Min(result, MaxFramesPerSecond);
        }
        catch (Exception)
        {
            // RTSS pode desaparecer, trocar de versão ou negar acesso entre amostras.
            return null;
        }
    }

    /// <summary>Não há handle persistente: cada leitura abre e fecha o mapeamento.</summary>
    public void Dispose()
    {
    }

    /// <summary>Copia o bloco do RTSS para um buffer gerenciado e libera os handles.</summary>
    private static byte[]? TryReadSharedMemory()
    {
        foreach (var name in MappingNames)
        {
            var mapping = OpenFileMapping(FileMapRead, false, name);
            if (mapping == IntPtr.Zero)
                continue;

            try
            {
                var view = MapViewOfFile(mapping, FileMapRead, 0, 0, 0);
                if (view == IntPtr.Zero)
                    continue;

                try
                {
                    var regionSize = QueryRegionSize(view);
                    if (regionSize == 0)
                        continue;

                    var length = (int)Math.Min(regionSize, MaxViewBytes);
                    var buffer = new byte[length];
                    Marshal.Copy(view, buffer, 0, length);
                    return buffer;
                }
                finally
                {
                    _ = UnmapViewOfFile(view);
                }
            }
            finally
            {
                _ = CloseHandle(mapping);
            }
        }

        return null;
    }

    /// <summary>Descobre o tamanho da região mapeada a partir do endereço base.</summary>
    private static ulong QueryRegionSize(IntPtr view)
    {
        var size = (nuint)Marshal.SizeOf<MemoryBasicInformation>();
        if (VirtualQuery(view, out var info, size) == 0)
            return 0;

        return info.RegionSize.ToUInt64();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenFileMapping(uint desiredAccess, bool inheritHandle, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(
        IntPtr fileMappingObject,
        uint desiredAccess,
        uint fileOffsetHigh,
        uint fileOffsetLow,
        nuint numberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(IntPtr baseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint VirtualQuery(
        IntPtr address,
        out MemoryBasicInformation buffer,
        nuint length);
}

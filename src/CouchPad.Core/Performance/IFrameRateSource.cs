namespace CouchPad.Core.Performance;

/// <summary>
/// Fonte de FPS por processo. A leitura é opcional: retorna <c>null</c> quando
/// o processo não expõe framerate ou a medição não está disponível.
/// </summary>
public interface IFrameRateSource : IDisposable
{
    /// <summary>
    /// Lê o FPS do processo informado. <paramref name="now"/> existe para manter
    /// a computação determinística em testes e evitar dependência de relógio.
    /// </summary>
    double? ReadFramesPerSecond(int processId, DateTimeOffset now);
}

namespace CouchPad.Core.Automation;

/// <summary>The rescue decision cannot be safely determined; no additional mutation is allowed.</summary>
public sealed class RescueDecisionUncertainException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

/// <summary>
/// Guarda transacional para operações que alteram display/áudio.
/// A implementação de plataforma inicia um watchdog independente; o Core só
/// orquestra begin/commit/abort/abandon.
/// </summary>
public interface IRescueGuard
{
    Task<IRescueLease> BeginAsync(
        string operation,
        CancellationToken cancellationToken = default);
}

public interface IRescueLease : IAsyncDisposable
{
    /// <summary>Sinalizado se o watchdog parar inesperadamente antes da liberação.</summary>
    CancellationToken FailureToken { get; }

    /// <summary>Verifica também a disponibilidade imediatamente, sem esperar o monitor assíncrono.</summary>
    void ThrowIfUnavailable();

    /// <summary>Operação aplicada e verificada; o watchdog pode encerrar.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rollback concluído; o watchdog pode encerrar sem restaurar.</summary>
    Task AbortAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Não foi possível garantir o rollback. Deixe o watchdog restaurar o
    /// baseline ao expirar.
    /// </summary>
    void Abandon();
}

/// <summary>Usado na simulação: não inicia processo nem altera nada.</summary>
public sealed class NoopRescueGuard : IRescueGuard
{
    public static NoopRescueGuard Instance { get; } = new();

    private NoopRescueGuard()
    {
    }

    public Task<IRescueLease> BeginAsync(
        string operation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IRescueLease>(NoopRescueLease.Instance);

    private sealed class NoopRescueLease : IRescueLease
    {
        public static NoopRescueLease Instance { get; } = new();

        public CancellationToken FailureToken => CancellationToken.None;

        public void ThrowIfUnavailable() { }

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AbortAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Abandon()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

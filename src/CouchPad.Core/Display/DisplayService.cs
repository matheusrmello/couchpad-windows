namespace CouchPad.Core.Display;

public enum DisplayMode
{
    TvOnly,
    Extend,
    Mirror,

    /// <summary>Baseline sem a TV: apenas o monitor interno está ativo.</summary>
    InternalOnly,
}

/// <summary>Estado abstrato do display, sem P/Invoke e sem nomes de tela.</summary>
public sealed record DisplayState(DisplayMode Mode, bool PrimaryActive, bool TvActive)
{
    /// <summary>
    /// Snapshot opaco da topologia completa, serializado pela implementação de
    /// plataforma (schema/versão próprio). O Core não interpreta o conteúdo;
    /// ele apenas persiste e devolve no restore.
    /// </summary>
    public string? Topology { get; init; }
}

public interface IDisplayService
{
    DisplayState GetCurrent();

    Task<DisplayState> ApplyAsync(DisplayMode mode, CancellationToken cancellationToken = default);

    Task RestoreAsync(DisplayState state, CancellationToken cancellationToken = default);
}

/// <summary>Revokes new display applies for the remainder of this process; restores remain available.</summary>
public interface IDisplayWriteControl
{
    bool CanApply { get; }
    void DisableNewWrites();
}

/// <summary>Identifies a failed apply that the platform proves never reached a display mutation.</summary>
public static class DisplayApplyFailure
{
    private const string UnchangedKey = "CouchPad.DisplayApply.Unchanged";
    public static void MarkUnchanged(Exception exception) => exception.Data[UnchangedKey] = true;
    public static bool IsUnchanged(Exception exception) => exception.Data[UnchangedKey] is true;
}

/// <summary>
/// Simulação: aplica o modo depois de uma latência curta para a UI mostrar
/// o estado "Aplicando". Nenhuma chamada CCD acontece aqui.
/// </summary>
public sealed class SimulatedDisplayService : IDisplayService
{
    private readonly TimeSpan _latency;

    public SimulatedDisplayService(TimeSpan? latency = null) =>
        _latency = latency is { } value && value > TimeSpan.Zero ? value : TimeSpan.Zero;

    public DisplayState Current { get; private set; } =
        new(DisplayMode.InternalOnly, PrimaryActive: true, TvActive: false);

    public DisplayState GetCurrent() => Current;

    public async Task<DisplayState> ApplyAsync(
        DisplayMode mode,
        CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);

        Current = mode switch
        {
            DisplayMode.TvOnly => new DisplayState(DisplayMode.TvOnly, PrimaryActive: false, TvActive: true),
            DisplayMode.Mirror => new DisplayState(DisplayMode.Mirror, PrimaryActive: true, TvActive: true),
            DisplayMode.InternalOnly => new DisplayState(DisplayMode.InternalOnly, PrimaryActive: true, TvActive: false),
            _ => new DisplayState(DisplayMode.Extend, PrimaryActive: true, TvActive: true),
        };

        return Current;
    }

    public async Task RestoreAsync(
        DisplayState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await DelayAsync(cancellationToken);
        Current = state;
    }

    private Task DelayAsync(CancellationToken cancellationToken) =>
        _latency > TimeSpan.Zero
            ? Task.Delay(_latency, cancellationToken)
            : Task.CompletedTask;
}

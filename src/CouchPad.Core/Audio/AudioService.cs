namespace CouchPad.Core.Audio;

public enum AudioMode
{
    MoveToHdmi,
    KeepCurrent,
}

public enum AudioOutput
{
    Default,
    Hdmi,
}

public interface IAudioService
{
    AudioOutput GetCurrent();

    Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken cancellationToken = default);

    Task RestoreAsync(AudioOutput state, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simulação do endpoint padrão. Nenhum acesso a Core Audio acontece aqui.
/// </summary>
public sealed class SimulatedAudioService : IAudioService
{
    private readonly TimeSpan _latency;

    public SimulatedAudioService(TimeSpan? latency = null) =>
        _latency = latency is { } value && value > TimeSpan.Zero ? value : TimeSpan.Zero;

    public AudioOutput Current { get; private set; } = AudioOutput.Default;

    public AudioOutput GetCurrent() => Current;

    public async Task<AudioOutput> ApplyAsync(
        AudioMode mode,
        CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);

        if (mode == AudioMode.MoveToHdmi)
        {
            Current = AudioOutput.Hdmi;
        }

        return Current;
    }

    public async Task RestoreAsync(
        AudioOutput state,
        CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);
        Current = state;
    }

    private Task DelayAsync(CancellationToken cancellationToken) =>
        _latency > TimeSpan.Zero
            ? Task.Delay(_latency, cancellationToken)
            : Task.CompletedTask;
}

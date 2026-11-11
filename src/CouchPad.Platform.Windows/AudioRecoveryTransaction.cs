using CouchPad.Core.Audio;

namespace CouchPad.Platform.Windows;

public enum AudioRole { Console, Multimedia, Communications }

/// <summary>Backend for the independently testable audio recovery sequence.</summary>
public interface IAudioRecoveryBackend
{
    bool IsActive(string endpointId);
    void SetDefault(string endpointId, AudioRole role);
    string? GetDefault(AudioRole role);
    void SetVolumeAndMute(string endpointId, float volume, bool muted);
    (float Volume, bool Muted) GetVolumeAndMute(string endpointId);
}

/// <summary>Restores a complete baseline; failures remain observable to the watchdog.</summary>
public static class AudioRecoveryTransaction
{
    /// <summary>Waits for endpoints to reappear after display recovery before attempting writes.</summary>
    public static async Task RestoreWhenAvailableAsync(AudioBaseline baseline, IAudioRecoveryBackend backend,
        TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(backend);
        if (timeout <= TimeSpan.Zero || pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        var endpoints = Validate(baseline);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (endpoints.Keys.All(backend.IsActive)) break;
            await Task.Delay(pollInterval, deadline.Token).ConfigureAwait(false);
        }
        Restore(baseline, backend, deadline.Token);
    }

    public static void Restore(AudioBaseline baseline, IAudioRecoveryBackend backend, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(backend);
        var endpoints = Validate(baseline);
        var roles = new[] { baseline.ConsoleEndpointId, baseline.MultimediaEndpointId, baseline.CommunicationsEndpointId };
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var id in endpoints.Keys)
            if (!backend.IsActive(id)) throw new InvalidOperationException("Baseline audio endpoint is unavailable.");
        for (var i = 0; i < roles.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            backend.SetDefault(roles[i]!, (AudioRole)i);
            if (!string.Equals(backend.GetDefault((AudioRole)i), roles[i], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Audio default verification failed.");
        }
        foreach (var endpoint in endpoints.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            backend.SetVolumeAndMute(endpoint.EndpointId, endpoint.MasterVolume!.Value, endpoint.Muted!.Value);
            var actual = backend.GetVolumeAndMute(endpoint.EndpointId);
            if (!float.IsFinite(actual.Volume) || Math.Abs(actual.Volume - endpoint.MasterVolume.Value) > 0.01f || actual.Muted != endpoint.Muted.Value)
                throw new InvalidOperationException("Audio volume verification failed.");
        }
    }

    private static Dictionary<string, AudioEndpointBaseline> Validate(AudioBaseline baseline)
    {
        if (baseline.SchemaVersion != 1 || baseline.Endpoints is null) throw new InvalidDataException("Unsupported audio baseline.");
        var roles = new[] { baseline.ConsoleEndpointId, baseline.MultimediaEndpointId, baseline.CommunicationsEndpointId };
        if (roles.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("Audio baseline is missing a role.");
        var endpoints = baseline.Endpoints.ToDictionary(e => e.EndpointId, StringComparer.OrdinalIgnoreCase);
        if (roles.Any(id => !endpoints.ContainsKey(id!)) || endpoints.Count != roles.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidDataException("Audio baseline endpoint set is inconsistent.");
        foreach (var endpoint in endpoints.Values)
        {
            if (string.IsNullOrWhiteSpace(endpoint.EndpointId) || endpoint.MasterVolume is null || endpoint.Muted is null ||
                !float.IsFinite(endpoint.MasterVolume.Value) || endpoint.MasterVolume.Value is < 0 or > 1)
                throw new InvalidDataException("Audio baseline volume or mute is invalid.");
        }
        return endpoints;
    }
}

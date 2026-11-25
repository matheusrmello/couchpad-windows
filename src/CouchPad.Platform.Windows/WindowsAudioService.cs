using System.Runtime.Versioning;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

public sealed record WindowsAudioOptions(
    string TvEndpointId,
    bool AllowWrites = false,
    TimeSpan? AvailabilityTimeout = null,
    string? TvEndpointName = null,
    string? TvDisplayName = null);

/// <summary>Explicit-endpoint audio service. Writes require an armed rescue transaction.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAudioService : IAudioService, IAudioBaselineService
{
    private readonly WindowsAudioOptions _options;
    private readonly RescueTransactionContext _transaction;
    private readonly IAudioRecoveryBackend _backend;
    private readonly Func<WindowsAudioProbe.Inventory> _inventory;
    private readonly Func<AudioBaseline> _baseline;
    private readonly CancellationTokenSource _writesRevoked = new();

    public WindowsAudioService(WindowsAudioOptions options, RescueTransactionContext transaction)
        : this(options, transaction, new WindowsAudioRecoveryBackend(options.AllowWrites), WindowsAudioProbe.Capture, WindowsAudioProbe.CaptureBaseline) { }

    internal WindowsAudioService(WindowsAudioOptions options, RescueTransactionContext transaction,
        IAudioRecoveryBackend backend, Func<WindowsAudioProbe.Inventory> inventory, Func<AudioBaseline> baseline)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TvEndpointId);
        if (options.AvailabilityTimeout is { } timeout && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2)))
            throw new ArgumentOutOfRangeException(nameof(options));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
    }

    public AudioBaseline CaptureBaseline() => _baseline();

    public AudioOutput GetCurrent()
    {
        var inventory = _inventory();
        if (ResolveEndpoint(inventory) is not { } endpoint)
        {
            return AudioOutput.Default;
        }

        return new[] { inventory.Console, inventory.Multimedia, inventory.Communications }
            .All(id => string.Equals(id, endpoint.Id, StringComparison.OrdinalIgnoreCase)) ? AudioOutput.Hdmi : AudioOutput.Default;
    }

    public async Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == AudioMode.KeepCurrent) return GetCurrent();
        RequireWrites();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _writesRevoked.Token);
        deadline.CancelAfter(_options.AvailabilityTimeout ?? TimeSpan.FromSeconds(15));
        try { return await ApplyAvailableAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && !_writesRevoked.IsCancellationRequested && deadline.IsCancellationRequested)
        { throw new AudioEndpointUnavailableException(_options.TvEndpointName, _options.TvEndpointId, ex); }
    }

    /// <summary>
    /// Active exact ID first; then the unique active endpoint with the saved name;
    /// then the unique active endpoint matching the TV display name token.
    /// </summary>
    private WindowsAudioProbe.Endpoint? ResolveEndpoint(WindowsAudioProbe.Inventory inventory) =>
        TvAudioEndpointResolver.Resolve(
            inventory.Endpoints, _options.TvEndpointId, _options.TvEndpointName, _options.TvDisplayName);

    private async Task<AudioOutput> ApplyAvailableAsync(CancellationToken token)
    {
        WindowsAudioProbe.Endpoint endpoint;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var endpoints = _inventory().Endpoints;
            var duplicates = endpoints.Count(e => e.State == 1 && string.Equals(e.Id, _options.TvEndpointId, StringComparison.OrdinalIgnoreCase));
            if (duplicates > 1) throw new InvalidOperationException("Audio endpoint identity is ambiguous.");
            if (TvAudioEndpointResolver.Resolve(
                    endpoints, _options.TvEndpointId, _options.TvEndpointName, _options.TvDisplayName) is { } resolved)
            {
                endpoint = resolved;
                break;
            }
            await Task.Delay(100, token).ConfigureAwait(false);
        }
        await using var held = await DisplayMutationLock.AcquireAsync(_transaction.MutationLockPath, token).ConfigureAwait(false);
        await _transaction.ExecuteIfStateAsync(RescueDecisionState.Armed, () =>
        {
            WindowsAudioProbe.SelectActiveById(_inventory().Endpoints, endpoint.Id);
            foreach (var role in Enum.GetValues<AudioRole>())
            {
                token.ThrowIfCancellationRequested();
                _backend.SetDefault(endpoint.Id, role);
            }
            foreach (var role in Enum.GetValues<AudioRole>())
                if (!string.Equals(_backend.GetDefault(role), endpoint.Id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Audio default verification failed.");
        }, token).ConfigureAwait(false);
        return AudioOutput.Hdmi;
    }

    public Task RestoreBaselineAsync(AudioBaseline baseline, CancellationToken cancellationToken = default)
    {
        RequireWrites();
        return GuardedAudioRecovery.RestoreAsync(baseline, _backend, _transaction,
            _options.AvailabilityTimeout ?? TimeSpan.FromSeconds(15), RescueDecisionState.Armed, cancellationToken);
    }

    public Task RestoreAsync(AudioOutput state, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("A complete audio baseline is required for restoration.");

    public void DisableNewWrites() => _writesRevoked.Cancel();

    private void RequireWrites() { if (!_options.AllowWrites) throw new NotSupportedException("Audio writes are disabled."); }
}

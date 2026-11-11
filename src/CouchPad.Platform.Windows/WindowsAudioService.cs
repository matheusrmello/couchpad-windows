using System.Runtime.Versioning;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

public sealed record WindowsAudioOptions(string TvEndpointId, bool AllowWrites = false, TimeSpan? AvailabilityTimeout = null);

/// <summary>Explicit-endpoint audio service. Writes require an armed rescue transaction.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAudioService : IAudioService, IAudioBaselineService
{
    private readonly WindowsAudioOptions _options;
    private readonly RescueTransactionContext _transaction;
    private readonly IAudioRecoveryBackend _backend;
    private readonly Func<WindowsAudioProbe.Inventory> _inventory;
    private readonly Func<AudioBaseline> _baseline;

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
        return new[] { inventory.Console, inventory.Multimedia, inventory.Communications }
            .All(id => string.Equals(id, _options.TvEndpointId, StringComparison.OrdinalIgnoreCase)) ? AudioOutput.Hdmi : AudioOutput.Default;
    }

    public async Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == AudioMode.KeepCurrent) return GetCurrent();
        RequireWrites();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.AvailabilityTimeout ?? TimeSpan.FromSeconds(15));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var matches = _inventory().Endpoints.Where(e => e.State == 1 && string.Equals(e.Id, _options.TvEndpointId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Audio endpoint identity is ambiguous.");
            if (matches.Length == 1) break;
            await Task.Delay(100, deadline.Token).ConfigureAwait(false);
        }
        await using var held = await DisplayMutationLock.AcquireAsync(_transaction.MutationLockPath, deadline.Token).ConfigureAwait(false);
        await _transaction.ExecuteIfStateAsync(RescueDecisionState.Armed, () =>
        {
            WindowsAudioProbe.SelectActiveById(_inventory().Endpoints, _options.TvEndpointId);
            foreach (var role in Enum.GetValues<AudioRole>())
            {
                deadline.Token.ThrowIfCancellationRequested();
                _backend.SetDefault(_options.TvEndpointId, role);
            }
            foreach (var role in Enum.GetValues<AudioRole>())
                if (!string.Equals(_backend.GetDefault(role), _options.TvEndpointId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Audio default verification failed.");
        }, deadline.Token).ConfigureAwait(false);
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

    private void RequireWrites() { if (!_options.AllowWrites) throw new NotSupportedException("Audio writes are disabled."); }
}

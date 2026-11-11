using CouchPad.Core.Audio;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Fences rescue audio writes against concurrent display and decision changes.</summary>
public static class GuardedAudioRecovery
{
    public static async Task RestoreAsync(AudioBaseline baseline, IAudioRecoveryBackend backend,
        RescueTransactionContext transaction, TimeSpan availabilityTimeout, CancellationToken cancellationToken = default)
        => await RestoreAsync(baseline, backend, transaction, availabilityTimeout, RescueDecisionState.RecoveryRequested, cancellationToken).ConfigureAwait(false);

    internal static async Task RestoreAsync(AudioBaseline baseline, IAudioRecoveryBackend backend,
        RescueTransactionContext transaction, TimeSpan availabilityTimeout, RescueDecisionState expectedDecision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        await AudioRecoveryTransaction.WaitForEndpointsAsync(baseline, backend, availabilityTimeout,
            TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        await using var mutation = await DisplayMutationLock.AcquireAsync(transaction.MutationLockPath, cancellationToken).ConfigureAwait(false);
        await transaction.ExecuteIfStateAsync(expectedDecision,
            () => AudioRecoveryTransaction.Restore(baseline, backend, cancellationToken), cancellationToken).ConfigureAwait(false);
    }
}

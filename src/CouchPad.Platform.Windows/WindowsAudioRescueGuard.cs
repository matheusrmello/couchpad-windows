using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Opt-in rescue guard whose watchdog understands the complete audio baseline.</summary>
public sealed class WindowsAudioRescueGuard : IAudioRescueGuard
{
    private readonly WindowsRescueGuard _guard;

    public WindowsAudioRescueGuard(string executable, string journal, TimeSpan timeout,
        RescueTransactionContext transaction, bool allowRecovery = false)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _guard = new WindowsRescueGuard(executable, journal, timeout, allowRecovery, transaction,
            allowExperimentalAudioRecovery: true);
    }

    public Task<IRescueLease> BeginAsync(string operation, CancellationToken cancellationToken = default) =>
        _guard.BeginAsync(operation, cancellationToken);
}

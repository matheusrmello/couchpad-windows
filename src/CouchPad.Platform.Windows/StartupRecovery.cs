using System.Text.Json;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Validates and reconciles a persisted session; callers must hold the session and watchdog locks.</summary>
internal static class StartupRecovery
{
    internal static async Task<bool> RunAsync(ISessionJournalStore journal,
        Func<SessionJournalEntry, CancellationToken, Task> restore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(restore);
        cancellationToken.ThrowIfCancellationRequested();
        var inspection = journal.Inspect();
        if (inspection.Status == SessionJournalStatus.Missing) return false;
        var entry = inspection.Entry;
        if (inspection.Status != SessionJournalStatus.Present || entry is null ||
            entry.State is not (SessionJournalEntry.PendingState or SessionJournalEntry.ActiveState))
            throw new InvalidDataException("The recovery journal cannot be trusted.");
        if (entry.RestoreDisplay?.Topology is null || entry.RestoreAudioBaseline is null)
            throw new InvalidDataException("A complete display and audio baseline is required.");
        var snapshot = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(entry.RestoreDisplay.Topology)
            ?? throw new InvalidDataException("The display baseline is missing.");
        if (!DisplaySnapshotComparer.SemanticallyEqual(snapshot, snapshot))
            throw new InvalidDataException("The display baseline is invalid.");
        AudioRecoveryTransaction.ValidateBaseline(entry.RestoreAudioBaseline);
        var original = JsonSerializer.Serialize(entry);

        await restore(entry, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var after = journal.Inspect();
        if (after.Status != SessionJournalStatus.Present || after.Entry is null ||
            !string.Equals(JsonSerializer.Serialize(after.Entry), original, StringComparison.Ordinal))
            throw new InvalidDataException("The journal changed during recovery.");
        journal.Clear();
        if (journal.Inspect().Status != SessionJournalStatus.Missing)
            throw new IOException("Recovery journal removal could not be verified.");
        return true;
    }
}

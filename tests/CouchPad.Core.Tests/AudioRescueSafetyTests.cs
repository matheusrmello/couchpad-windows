using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class AudioRescueSafetyTests
{
    [Fact]
    public async Task Display_only_guard_refuses_real_audio_journal_before_starting_child()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "couchpad-audio-guard-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "session.json");
        try
        {
            var journal = new JsonSessionJournalStore(path);
            journal.Save(new SessionJournalEntry(SessionJournalEntry.PendingState, DisplayMode.TvOnly, AudioMode.MoveToHdmi, DateTimeOffset.UtcNow)
            {
                RestoreDisplay = new DisplayState(DisplayMode.InternalOnly, true, false) { Topology = "snapshot" },
                RestoreAudioBaseline = new AudioBaseline(1, "a", "b", "c", []),
            });
            var guard = new WindowsRescueGuard(Path.Combine(directory, "missing.exe"), path, TimeSpan.FromSeconds(10), allowRecovery: true);
            await Assert.ThrowsAsync<NotSupportedException>(() => guard.BeginAsync("test"));
            Assert.Equal(SessionJournalEntry.PendingState, journal.Load()!.State);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}

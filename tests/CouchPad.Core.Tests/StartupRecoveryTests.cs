using System.Text.Json;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class StartupRecoveryTests
{
    [Theory]
    [InlineData(SessionJournalEntry.PendingState)]
    [InlineData(SessionJournalEntry.ActiveState)]
    public async Task Clears_only_after_a_successful_restore(string state)
    {
        var store = new Store { Entry = Entry(state) };
        var called = false;
        Assert.True(await StartupRecovery.RunAsync(store, (entry, _) =>
        {
            Assert.Same(store.Entry, entry);
            Assert.False(store.Cleared);
            called = true;
            return Task.CompletedTask;
        }));
        Assert.True(called);
        Assert.True(store.Cleared);
    }

    [Fact]
    public async Task Missing_journal_does_not_restore() =>
        Assert.False(await StartupRecovery.RunAsync(new Store(), (_, _) => throw new Exception()));

    [Fact]
    public async Task Failed_restore_preserves_journal()
    {
        var store = new Store { Entry = Entry(SessionJournalEntry.ActiveState) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartupRecovery.RunAsync(store,
            (_, _) => Task.FromException(new InvalidOperationException())));
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Failed_clear_reports_failure_and_preserves_journal()
    {
        var store = new Store { Entry = Entry(SessionJournalEntry.ActiveState), FailClear = true };
        await Assert.ThrowsAsync<IOException>(() => StartupRecovery.RunAsync(store, (_, _) => Task.CompletedTask));
        Assert.NotNull(store.Entry);
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Journal_becoming_unreadable_after_restore_is_not_cleared()
    {
        var store = new Store { Entry = Entry(SessionJournalEntry.PendingState) };
        await Assert.ThrowsAsync<InvalidDataException>(() => StartupRecovery.RunAsync(store, (_, _) =>
        {
            store.Unreadable = true;
            return Task.CompletedTask;
        }));
        Assert.NotNull(store.Entry);
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Changed_journal_is_not_cleared()
    {
        var store = new Store { Entry = Entry(SessionJournalEntry.PendingState) };
        await Assert.ThrowsAsync<InvalidDataException>(() => StartupRecovery.RunAsync(store, (_, _) =>
        {
            store.Entry = Entry(SessionJournalEntry.ActiveState);
            return Task.CompletedTask;
        }));
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Incomplete_audio_baseline_blocks_writes()
    {
        var store = new Store { Entry = Entry(SessionJournalEntry.PendingState) with { RestoreAudioBaseline = null } };
        await Assert.ThrowsAsync<InvalidDataException>(() => StartupRecovery.RunAsync(store, (_, _) => throw new Exception()));
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Invalid_display_baseline_blocks_writes()
    {
        var original = Entry(SessionJournalEntry.PendingState);
        var store = new Store { Entry = original with { RestoreDisplay = original.RestoreDisplay! with { Topology = "{}" } } };
        var called = false;
        await Assert.ThrowsAnyAsync<Exception>(() => StartupRecovery.RunAsync(store, (_, _) => { called = true; return Task.CompletedTask; }));
        Assert.False(called);
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Unknown_state_blocks_writes()
    {
        var store = new Store { Entry = Entry("unknown") };
        await Assert.ThrowsAsync<InvalidDataException>(() => StartupRecovery.RunAsync(store, (_, _) => throw new Exception()));
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Unreadable_journal_blocks_writes()
    {
        var store = new Store { Unreadable = true };
        await Assert.ThrowsAsync<InvalidDataException>(() => StartupRecovery.RunAsync(store, (_, _) => throw new Exception()));
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Cancellation_after_restore_preserves_journal()
    {
        var store = new Store { Entry = Entry(SessionJournalEntry.ActiveState) };
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StartupRecovery.RunAsync(store, (_, _) =>
        {
            cancel.Cancel();
            return Task.CompletedTask;
        }, cancel.Token));
        Assert.False(store.Cleared);
    }

    private static SessionJournalEntry Entry(string state)
    {
        var path = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(path, 32); // target mode index
        BitConverter.GetBytes(1u).CopyTo(path, 68); // active
        var modes = new byte[128];
        BitConverter.GetBytes(1u).CopyTo(modes, 0);
        BitConverter.GetBytes(2u).CopyTo(modes, 64);
        var snapshot = new WindowsDisplayService.Snapshot(1, 2, 72, 64,
            Convert.ToBase64String(path), Convert.ToBase64String(modes), []);
        var audio = new AudioBaseline(1, "monitor", "monitor", "monitor", [new("monitor", .5f, false)]);
        return new(state, DisplayMode.TvOnly, AudioMode.MoveToHdmi, DateTimeOffset.UtcNow)
        {
            RestoreDisplay = new(DisplayMode.InternalOnly, true, false) { Topology = JsonSerializer.Serialize(snapshot) },
            RestoreAudioBaseline = audio,
        };
    }

    private sealed class Store : ISessionJournalStore
    {
        public SessionJournalEntry? Entry;
        public bool Cleared;
        public bool Unreadable;
        public bool FailClear;
        public SessionJournalEntry? Load() => Entry;
        public SessionJournalInspection Inspect() => Unreadable ? new(SessionJournalStatus.Unreadable, null) : Entry is null ? new(SessionJournalStatus.Missing, null) : new(SessionJournalStatus.Present, Entry);
        public void Save(SessionJournalEntry entry) => Entry = entry;
        public void Clear() { if (FailClear) throw new IOException("Injected clear failure"); Cleared = true; Entry = null; }
    }
}

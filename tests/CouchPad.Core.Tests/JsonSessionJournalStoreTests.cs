using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;

namespace CouchPad.Core.Tests;

public sealed class JsonSessionJournalStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "couchpad-tests", Guid.NewGuid().ToString("N"));

    private string JournalPath => Path.Combine(_directory, "state", "session.json");

    [Fact]
    public void Load_missing_file_returns_null()
    {
        var store = new JsonSessionJournalStore(JournalPath);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var store = new JsonSessionJournalStore(JournalPath);
        var expected = new SessionJournalEntry(
            SessionJournalEntry.ActiveState,
            DisplayMode.Mirror,
            AudioMode.MoveToHdmi,
            new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero))
        {
            RestoreDisplay = new DisplayState(DisplayMode.InternalOnly, PrimaryActive: true, TvActive: false)
            {
                Topology = "{\"schema\":1,\"paths\":[]}",
            },
            RestoreAudio = AudioOutput.Default,
            AppliedAudio = AudioOutput.Hdmi,
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Load_old_entry_without_baseline_returns_nulls()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
        File.WriteAllText(
            JournalPath,
            """
            {
              "state": "active",
              "displayMode": "tvOnly",
              "audioMode": "moveToHdmi",
              "updatedAt": "2026-11-09T12:00:00+00:00"
            }
            """);
        var store = new JsonSessionJournalStore(JournalPath);

        var entry = store.Load();

        Assert.NotNull(entry);
        Assert.Null(entry!.RestoreDisplay);
        Assert.Null(entry.RestoreAudio);
    }

    [Fact]
    public void Load_corrupt_json_returns_null_and_backs_up_file()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
        File.WriteAllText(JournalPath, "{ not json");
        var store = new JsonSessionJournalStore(JournalPath);

        var entry = store.Load();

        Assert.Null(entry);
        Assert.True(File.Exists(JournalPath + ".bak"));
        Assert.Equal("{ not json", File.ReadAllText(JournalPath + ".bak"));
    }

    [Fact]
    public void Clear_removes_file()
    {
        var store = new JsonSessionJournalStore(JournalPath);
        store.Save(new SessionJournalEntry(
            SessionJournalEntry.ActiveState,
            DisplayMode.TvOnly,
            AudioMode.MoveToHdmi,
            DateTimeOffset.UtcNow));

        store.Clear();

        Assert.False(File.Exists(JournalPath));
        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_leaves_no_temp_file_behind()
    {
        var store = new JsonSessionJournalStore(JournalPath);
        store.Save(new SessionJournalEntry(
            SessionJournalEntry.ActiveState,
            DisplayMode.TvOnly,
            AudioMode.MoveToHdmi,
            DateTimeOffset.UtcNow));

        Assert.True(File.Exists(JournalPath));
        Assert.False(File.Exists(JournalPath + ".tmp"));
    }

    [Fact]
    public void Save_creates_missing_directory()
    {
        var store = new JsonSessionJournalStore(JournalPath);

        store.Save(new SessionJournalEntry(
            SessionJournalEntry.ActiveState,
            DisplayMode.TvOnly,
            AudioMode.MoveToHdmi,
            DateTimeOffset.UtcNow));

        Assert.True(File.Exists(JournalPath));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Limpeza de teste não deve falhar a suíte.
        }
    }
}

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
    public void Inspect_missing_file_is_distinct_from_present_journal()
    {
        var store = new JsonSessionJournalStore(JournalPath);
        Assert.Equal(SessionJournalStatus.Missing, store.Inspect().Status);
        var expected = new SessionJournalEntry(SessionJournalEntry.PendingState, DisplayMode.TvOnly, AudioMode.KeepCurrent, DateTimeOffset.UtcNow);
        store.Save(expected);
        var inspection = store.Inspect();
        Assert.Equal(SessionJournalStatus.Present, inspection.Status);
        Assert.Equal(expected, inspection.Entry);
    }

    [Theory]
    [InlineData("{ invalid")]
    [InlineData("null")]
    public void Inspect_corrupt_or_null_json_preserves_original(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
        File.WriteAllText(JournalPath, contents);
        var inspection = new JsonSessionJournalStore(JournalPath).Inspect();
        Assert.Equal(SessionJournalStatus.Unreadable, inspection.Status);
        Assert.Equal(contents, File.ReadAllText(JournalPath));
        Assert.False(File.Exists(JournalPath + ".bak"));
    }

    [Fact]
    public void Inspect_locked_file_is_unreadable_not_missing()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(Path.GetDirectoryName(JournalPath)!);
        File.WriteAllText(JournalPath, "{}");
        using var held = new FileStream(JournalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var inspection = new JsonSessionJournalStore(JournalPath).Inspect();
        Assert.Equal(SessionJournalStatus.Unreadable, inspection.Status);
        Assert.True(File.Exists(JournalPath));
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
        Assert.Null(entry.RestoreAudioBaseline);
    }

    [Fact]
    public void Audio_baseline_roundtrips_distinct_roles_and_volume()
    {
        var store = new JsonSessionJournalStore(JournalPath);
        var expected = new AudioBaseline(1, "console-id", "music-id", "communications-id",
            [new AudioEndpointBaseline("console-id", 0.42f, false), new AudioEndpointBaseline("music-id", null, true)]);
        store.Save(new SessionJournalEntry(SessionJournalEntry.PendingState, DisplayMode.TvOnly, AudioMode.MoveToHdmi, DateTimeOffset.UtcNow)
        { RestoreAudioBaseline = expected });
        var actual = store.Load()!.RestoreAudioBaseline!;
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.ConsoleEndpointId, actual.ConsoleEndpointId);
        Assert.Equal(expected.MultimediaEndpointId, actual.MultimediaEndpointId);
        Assert.Equal(expected.CommunicationsEndpointId, actual.CommunicationsEndpointId);
        Assert.Equal(expected.Endpoints, actual.Endpoints);
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
    public void Clear_propagates_sharing_failure()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new JsonSessionJournalStore(JournalPath);
        store.Save(new SessionJournalEntry(SessionJournalEntry.ActiveState, DisplayMode.TvOnly, AudioMode.KeepCurrent, DateTimeOffset.UtcNow));
        using var held = new FileStream(JournalPath, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.ThrowsAny<IOException>(() => store.Clear());
        Assert.True(File.Exists(JournalPath));
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

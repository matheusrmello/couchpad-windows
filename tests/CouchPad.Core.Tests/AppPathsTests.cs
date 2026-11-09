using CouchPad.Core.Storage;

namespace CouchPad.Core.Tests;

public sealed class AppPathsTests
{
    private const string LocalAppData = @"C:\Users\example\AppData\Local";

    [Fact]
    public void GetRoot_appends_app_folder() =>
        Assert.Equal(
            Path.Combine(LocalAppData, "CouchPad"),
            AppPaths.GetRoot(LocalAppData));

    [Fact]
    public void SettingsFile_lives_in_root() =>
        Assert.Equal(
            Path.Combine(LocalAppData, "CouchPad", "settings.json"),
            AppPaths.SettingsFile(AppPaths.GetRoot(LocalAppData)));

    [Fact]
    public void SessionJournal_lives_in_state_folder() =>
        Assert.Equal(
            Path.Combine(LocalAppData, "CouchPad", "state", "session.json"),
            AppPaths.SessionJournalFile(AppPaths.GetRoot(LocalAppData)));

    [Fact]
    public void Logs_live_in_logs_folder() =>
        Assert.Equal(
            Path.Combine(LocalAppData, "CouchPad", "logs"),
            AppPaths.LogsDirectory(AppPaths.GetRoot(LocalAppData)));

    [Fact]
    public void GetRoot_rejects_empty_input() =>
        Assert.Throws<ArgumentException>(() => AppPaths.GetRoot(" "));
}

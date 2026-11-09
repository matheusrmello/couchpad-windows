using CouchPad.Core.Settings;

namespace CouchPad.Core.Tests;

public sealed class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "couchpad-tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var store = new JsonAppSettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Null(settings.Language);
        Assert.False(settings.AutomationEnabled);
        Assert.False(settings.StartWithWindows);
        Assert.True(settings.MinimizeToTray);
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var store = new JsonAppSettingsStore(SettingsPath);
        var expected = new AppSettings
        {
            Language = "pt-BR",
            AutomationEnabled = true,
            StartWithWindows = true,
            MinimizeToTray = false,
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Load_corrupt_json_returns_defaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ not json");
        var store = new JsonAppSettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Null(settings.Language);
        Assert.False(settings.AutomationEnabled);
    }

    [Fact]
    public void Save_leaves_no_temp_file_behind()
    {
        var store = new JsonAppSettingsStore(SettingsPath);

        store.Save(new AppSettings { Language = "en" });

        Assert.True(File.Exists(SettingsPath));
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void Save_creates_missing_directory()
    {
        var nested = Path.Combine(_directory, "nested", "deeper", "settings.json");
        var store = new JsonAppSettingsStore(nested);

        store.Save(new AppSettings());

        Assert.True(File.Exists(nested));
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

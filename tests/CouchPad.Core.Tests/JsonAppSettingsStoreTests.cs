using System.Text.Json;
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
        Assert.False(settings.UseWindowsGamepad);
        Assert.True(settings.MinimizeToTray);
        Assert.True(settings.CloseLauncherOnActivate);
        Assert.True(settings.SteamEnabled);
        Assert.Null(settings.CustomLauncherPath);
        Assert.Equal(2, settings.ActivationDelayIndex);
        Assert.Equal(0, settings.DeactivationDelayIndex);
        Assert.Equal(0, settings.DisplayModeIndex);
        Assert.Equal(0, settings.AudioModeIndex);
        Assert.False(settings.AllowDisplayWrites);
        Assert.False(settings.AllowAudioWrites);
        Assert.Null(settings.TvAudioEndpointId);
        Assert.Null(settings.TvDevicePath);
        Assert.Null(settings.TvEdidManufacturer);
        Assert.Null(settings.TvEdidProduct);
        Assert.Null(settings.TvExactName);
        Assert.Null(settings.InternalDevicePath);
        Assert.Null(settings.InternalEdidManufacturer);
        Assert.Null(settings.InternalEdidProduct);
        Assert.Null(settings.InternalExactName);
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
            UseWindowsGamepad = true,
            MinimizeToTray = false,
            CloseLauncherOnActivate = false,
            SteamEnabled = false,
            CustomLauncherPath = @"C:\Games\Launcher",
            ActivationDelayIndex = 2,
            DeactivationDelayIndex = 3,
            DisplayModeIndex = 1,
            AudioModeIndex = 1,
            AllowDisplayWrites = true,
            AllowAudioWrites = true,
            TvAudioEndpointId = "endpoint-tv",
            TvDevicePath = @"\\?\DISPLAY#SAM7167#tv",
            TvEdidManufacturer = 11596,
            TvEdidProduct = 29031,
            TvExactName = "SAMSUNG",
            InternalDevicePath = @"\\?\DISPLAY#AOC2702#monitor",
            InternalEdidManufacturer = 58117,
            InternalEdidProduct = 9986,
            InternalExactName = "27G2G4",
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Load_corrupt_json_returns_defaults_and_backs_up_file()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ not json");
        var store = new JsonAppSettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Null(settings.Language);
        Assert.False(settings.AutomationEnabled);
        Assert.True(File.Exists(SettingsPath + ".bak"));
        Assert.Equal("{ not json", File.ReadAllText(SettingsPath + ".bak"));
    }

    [Fact]
    public void Load_accepts_trailing_commas_and_comments()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            SettingsPath,
            """
            {
              // comentário permitido em configuração local
              "language": "pt-BR",
              "automationEnabled": true,
            }
            """);
        var store = new JsonAppSettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Equal("pt-BR", settings.Language);
        Assert.True(settings.AutomationEnabled);
    }

    [Fact]
    public void Save_preserves_unknown_fields()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            SettingsPath,
            """{"language":"en","futureKey":"keep-me","futureNumber":42}""");
        var store = new JsonAppSettingsStore(SettingsPath);

        var loaded = store.Load();
        store.Save(loaded with { AutomationEnabled = true });

        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal("en", document.RootElement.GetProperty("language").GetString());
        Assert.True(document.RootElement.GetProperty("automationEnabled").GetBoolean());
        Assert.Equal("keep-me", document.RootElement.GetProperty("futureKey").GetString());
        Assert.Equal(42, document.RootElement.GetProperty("futureNumber").GetInt32());
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

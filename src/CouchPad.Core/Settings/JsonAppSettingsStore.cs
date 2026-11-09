using System.Text.Json;
using System.Text.Json.Serialization;
using CouchPad.Core.Storage;

namespace CouchPad.Core.Settings;

public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

/// <summary>
/// Gravação atômica: escreve em .tmp e move sobre o arquivo final.
/// Arquivo ausente ou corrompido volta para os padrões, sem derrubar o app.
/// Um arquivo corrompido é copiado para .bak antes de ser substituído.
/// </summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;

    public JsonAppSettingsStore() : this(AppPaths.SettingsFile())
    {
    }

    public JsonAppSettingsStore(string path) =>
        _path = path ?? throw new ArgumentNullException(nameof(path));

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                ?? new AppSettings();
        }
        catch (JsonException)
        {
            TryBackupCorruptFile();
            return new AppSettings();
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, SerializerOptions));
        File.Move(temp, _path, overwrite: true);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Copy(_path, _path + ".bak", overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Backup é melhor esforço; nunca impede o app de abrir.
        }
    }
}

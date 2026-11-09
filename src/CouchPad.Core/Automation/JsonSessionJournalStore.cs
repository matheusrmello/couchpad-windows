using System.Text.Json;
using System.Text.Json.Serialization;
using CouchPad.Core.Storage;

namespace CouchPad.Core.Automation;

/// <summary>
/// Journal atômico em %LOCALAPPDATA%\CouchPad\state\session.json.
/// Falha de leitura/escrita nunca derruba o fluxo de automação.
/// </summary>
public sealed class JsonSessionJournalStore : ISessionJournalStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _path;

    public JsonSessionJournalStore() : this(AppPaths.SessionJournalFile())
    {
    }

    public JsonSessionJournalStore(string path) =>
        _path = path ?? throw new ArgumentNullException(nameof(path));

    public SessionJournalEntry? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<SessionJournalEntry>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            TryBackupCorruptFile();
            return null;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(SessionJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(entry, SerializerOptions));
        File.Move(temp, _path, overwrite: true);
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Limpeza é melhor esforço; o rescue da P6 lida com estado obsoleto.
        }
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
            // Backup é melhor esforço.
        }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using CouchPad.Core.Storage;

namespace CouchPad.Core.Automation;

/// <summary>
/// Journal atômico em %LOCALAPPDATA%\CouchPad\state\session.json.
/// A inspeção distingue ausência de erro; falhas de escrita e limpeza são propagadas.
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

    /// <summary>Distinguishes missing from unreadable without modifying the journal.</summary>
    public SessionJournalInspection Inspect()
    {
        try
        {
            // File.Exists suppresses access errors; read directly so an unreadable
            // recovery record is never mistaken for an absent one.
            var json = File.ReadAllText(_path);
            var entry = JsonSerializer.Deserialize<SessionJournalEntry>(json, SerializerOptions);
            return entry is null
                ? new(SessionJournalStatus.Unreadable, null)
                : new(SessionJournalStatus.Present, entry);
        }
        catch (FileNotFoundException) { return new(SessionJournalStatus.Missing, null); }
        catch (DirectoryNotFoundException) { return new(SessionJournalStatus.Missing, null); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return new(SessionJournalStatus.Unreadable, null);
        }
    }

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
        // File.Delete already succeeds if the file does not exist. Do not use
        // File.Exists: it can turn access errors into a false success.
        File.Delete(_path);
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

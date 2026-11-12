using System.Text.Json;

namespace CouchPad.Platform.Windows;

/// <summary>Bounds automatic retries for one baseline, including interrupted attempts.</summary>
internal sealed class StartupRecoveryAttempts(string path)
{
    private sealed record Record(int Schema, string Baseline, int Count);
    internal void Begin(string baseline)
    {
        if (baseline.Length != 64 || !baseline.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid baseline hash.", nameof(baseline));
        Record? current;
        try { current = JsonSerializer.Deserialize<Record>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid recovery attempt record."); }
        catch (FileNotFoundException) { current = null; }
        catch (DirectoryNotFoundException) { current = null; }
        catch (JsonException ex) { throw new InvalidDataException("Invalid recovery attempt record.", ex); }
        if (current is not null && (current.Schema != 1 || current.Baseline is null || current.Baseline.Length != 64 ||
            !current.Baseline.All(Uri.IsHexDigit) || current.Count is < 1 or > 3))
            throw new InvalidDataException("Invalid recovery attempt record.");
        var count = current is not null && string.Equals(current.Baseline, baseline, StringComparison.OrdinalIgnoreCase) ? current.Count : 0;
        if (count >= 3) throw new InvalidOperationException("Automatic recovery retry limit reached.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, new Record(1, baseline.ToUpperInvariant(), count + 1));
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal void Clear() => File.Delete(path);
}

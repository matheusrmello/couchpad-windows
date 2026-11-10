using System.Globalization;

namespace CouchPad.Platform.Windows;

/// <summary>Best-effort local diagnostic; caller retains the recovery journal on failure.</summary>
public static class RescueFailureLog
{
    public static void TryWrite(string directory, string stage, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            Directory.CreateDirectory(directory);
            var safeStage = stage is "startup" or "journal" or "connect" or "protocol" or "restore" ? stage : "unknown";
            var record = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.UtcNow:O} stage={safeStage} error={error.GetType().Name} hresult=0x{error.HResult:X8}{Environment.NewLine}");
            File.AppendAllText(Path.Combine(directory, "rescue-failures.log"), record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Recovery failure must not be hidden by a logging failure.
        }
    }
}

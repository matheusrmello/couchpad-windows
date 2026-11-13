using System.Globalization;
using System.ComponentModel;

namespace CouchPad.Platform.Windows;

/// <summary>Best-effort local diagnostic; caller retains the recovery journal on failure.</summary>
public static class RescueFailureLog
{
    internal const string NativeOperationKey = "CouchPad.NativeOperation";

    public static void TryWrite(string directory, string stage, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            Directory.CreateDirectory(directory);
            var safeStage = stage is "startup" or "journal" or "connect" or "protocol" or "restore" ? stage : "unknown";
            var native = string.Concat(NativeDetails(error));
            var record = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.UtcNow:O} stage={safeStage} error={error.GetType().Name} hresult=0x{error.HResult:X8}{native}{Environment.NewLine}");
            File.AppendAllText(Path.Combine(directory, "rescue-failures.log"), record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Recovery failure must not be hidden by a logging failure.
        }
    }

    private static IEnumerable<string> NativeDetails(Exception error)
    {
        if (error is Win32Exception native)
        {
            var operation = native.Data[NativeOperationKey] as string;
            if (operation is not ("GetDisplayConfigBufferSizes" or "QueryDisplayConfig" or "SetDisplayConfig.Validate" or "SetDisplayConfig.Apply"))
                operation = "unknown";
            yield return string.Create(CultureInfo.InvariantCulture, $" api={operation} win32={native.NativeErrorCode}");
        }

        if (error is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            foreach (var detail in NativeDetails(inner))
                yield return detail;
        }
        else if (error.InnerException is { } inner)
        {
            foreach (var detail in NativeDetails(inner))
                yield return detail;
        }
    }
}

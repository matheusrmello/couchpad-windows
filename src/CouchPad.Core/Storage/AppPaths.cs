namespace CouchPad.Core.Storage;

/// <summary>
/// D015: tudo por usuário, sem admin, em %LOCALAPPDATA%\CouchPad.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "CouchPad";
    public const string SettingsFileName = "settings.json";
    public const string StateFolderName = "state";
    public const string SessionJournalFileName = "session.json";
    public const string LogsFolderName = "logs";

    public static string GetRoot() =>
        GetRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string GetRoot(string localAppData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppData);
        return Path.Combine(localAppData, AppFolderName);
    }

    public static string SettingsFile() => SettingsFile(GetRoot());

    public static string SettingsFile(string root) => Path.Combine(root, SettingsFileName);

    public static string StateDirectory() => StateDirectory(GetRoot());

    public static string StateDirectory(string root) => Path.Combine(root, StateFolderName);

    public static string SessionJournalFile() => SessionJournalFile(GetRoot());

    public static string SessionJournalFile(string root) =>
        Path.Combine(StateDirectory(root), SessionJournalFileName);

    public static string LogsDirectory() => LogsDirectory(GetRoot());

    public static string LogsDirectory(string root) => Path.Combine(root, LogsFolderName);
}

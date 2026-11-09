namespace CouchPad.Core.Settings;

/// <summary>
/// D016: automação desligada no primeiro uso.
/// </summary>
public sealed record AppSettings
{
    public string? Language { get; init; }

    public bool AutomationEnabled { get; init; }

    public bool StartWithWindows { get; init; }

    public bool MinimizeToTray { get; init; } = true;
}

using CouchPad.Core.Audio;
using CouchPad.Core.Display;
using CouchPad.Core.Launchers;

namespace CouchPad.Core.Automation;

/// <summary>Configuração que a state machine consome. Sem hardware.</summary>
public sealed record AutomationSettings
{
    public bool Enabled { get; init; }

    public TimeSpan ActivationDelay { get; init; } = TimeSpan.Zero;

    public TimeSpan DeactivationDelay { get; init; } = TimeSpan.Zero;

    public DisplayMode DisplayMode { get; init; } = DisplayMode.TvOnly;

    /// <summary>Resolução/Hz/escala desejados para a TV; auto deixa o Windows escolher.</summary>
    public DisplayPreference DisplayPreference { get; init; } = DisplayPreference.Auto;

    public AudioMode AudioMode { get; init; } = AudioMode.MoveToHdmi;

    public bool CloseLauncherOnActivate { get; init; } = true;

    public bool LauncherEnabled { get; init; }

    public LauncherKind Launcher { get; init; } = LauncherKind.Steam;
}

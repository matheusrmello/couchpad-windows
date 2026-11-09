using CouchPad.Core.Audio;
using CouchPad.Core.Display;

namespace CouchPad.Core.Automation;

/// <summary>
/// Registro do último estado aplicado. A P3 grava/limpa o journal; a P6 usa
/// o mesmo arquivo para o rescue no logon.
/// </summary>
public sealed record SessionJournalEntry(
    string State,
    DisplayMode DisplayMode,
    AudioMode AudioMode,
    DateTimeOffset UpdatedAt)
{
    public const string ActiveState = "active";

    /// <summary>Baseline anterior, alvo do restore da P6.</summary>
    public DisplayState? RestoreDisplay { get; init; }

    public AudioOutput? RestoreAudio { get; init; }
}

public interface ISessionJournalStore
{
    SessionJournalEntry? Load();

    void Save(SessionJournalEntry entry);

    void Clear();
}

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

    /// <summary>
    /// Gravado antes de qualquer mudança de topologia. Se o processo cair
    /// durante o apply, a P6 encontra este estado e restaura o baseline.
    /// </summary>
    public const string PendingState = "pending";

    /// <summary>Baseline anterior, alvo do restore da P6.</summary>
    public DisplayState? RestoreDisplay { get; init; }

    public AudioOutput? RestoreAudio { get; init; }

    /// <summary>Saída aplicada; nulo no estado pending.</summary>
    public AudioOutput? AppliedAudio { get; init; }
}

public interface ISessionJournalStore
{
    SessionJournalEntry? Load();

    void Save(SessionJournalEntry entry);

    void Clear();
}

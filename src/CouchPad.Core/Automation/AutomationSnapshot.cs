using CouchPad.Core.Audio;
using CouchPad.Core.Display;

namespace CouchPad.Core.Automation;

/// <summary>
/// Estado aplicado pela automação. Guarda também o estado anterior, que é o
/// alvo da restauração — nunca o estado aplicado.
/// </summary>
public sealed record AutomationSnapshot(
    DisplayState RestoreDisplay,
    AudioOutput RestoreAudio,
    DisplayMode DisplayMode,
    AudioMode AudioMode,
    AudioOutput AppliedAudio);

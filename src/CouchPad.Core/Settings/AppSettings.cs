using System.Text.Json;
using System.Text.Json.Serialization;

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

    public bool CloseLauncherOnActivate { get; init; } = true;

    public bool SteamEnabled { get; init; } = true;

    public string? CustomLauncherPath { get; init; }

    public int ActivationDelayIndex { get; init; } = 2;

    public int DeactivationDelayIndex { get; init; }

    public int DisplayModeIndex { get; init; }

    public int AudioModeIndex { get; init; }

    /// <summary>
    /// Opt-in explícito para escrita de display. Desligado por padrão; quando
    /// ligado, a composição também habilita o rescue guard.
    /// </summary>
    public bool AllowDisplayWrites { get; init; }

    /// <summary>Audio endpoint writes require a separate opt-in and a selected endpoint ID.</summary>
    public bool AllowAudioWrites { get; init; }

    public string? TvAudioEndpointId { get; init; }

    public string? TvDevicePath { get; init; }

    public ushort? TvEdidManufacturer { get; init; }

    public ushort? TvEdidProduct { get; init; }

    public string? TvExactName { get; init; }

    public string? InternalDevicePath { get; init; }

    public ushort? InternalEdidManufacturer { get; init; }

    public ushort? InternalEdidProduct { get; init; }

    public string? InternalExactName { get; init; }

    /// <summary>
    /// Preserva chaves desconhecidas ao regravar o arquivo, para não perder
    /// configurações de versões futuras ou de outros componentes.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }
}

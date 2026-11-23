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
    public bool CheckForUpdates { get; init; } = true;
    public bool IncludePreviewUpdates { get; init; }

    public bool UseWindowsGamepad { get; init; }

    public bool MinimizeToTray { get; init; } = true;

    public bool CloseLauncherOnActivate { get; init; } = true;

    public bool SteamEnabled { get; init; } = true;

    public int LauncherIndex { get; init; }

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
    /// Preferência de resolução/Hz/escala da TV espelhada do perfil ativo.
    /// Nulos = automático (Windows escolhe). Validados ao carregar o arquivo.
    /// </summary>
    public int? TvWidth { get; init; }

    public int? TvHeight { get; init; }

    public int? TvRefreshHz { get; init; }

    public int? TvScalePercent { get; init; }

    /// <summary>
    /// Active named hardware profile. The top-level TV/internal/audio fields are
    /// kept mirrored to this profile so startup composition keeps the existing
    /// single-configuration contract.
    /// </summary>
    public string? ActiveProfileId { get; init; }

    public List<EnvironmentProfile>? EnvironmentProfiles { get; init; }

    /// <summary>
    /// Preserva chaves desconhecidas ao regravar o arquivo, para não perder
    /// configurações de versões futuras ou de outros componentes.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }
}

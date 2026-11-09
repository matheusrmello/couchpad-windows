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

    /// <summary>
    /// Preserva chaves desconhecidas ao regravar o arquivo, para não perder
    /// configurações de versões futuras ou de outros componentes.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }
}

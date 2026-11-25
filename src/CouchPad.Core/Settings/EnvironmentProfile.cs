namespace CouchPad.Core.Settings;

/// <summary>Named display/audio selection for one room or setup.</summary>
public sealed record EnvironmentProfile
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string? TvAudioEndpointId { get; init; }

    public string? TvAudioEndpointName { get; init; }

    public string? TvDevicePath { get; init; }

    public ushort? TvEdidManufacturer { get; init; }

    public ushort? TvEdidProduct { get; init; }

    public string? TvExactName { get; init; }

    public string? InternalDevicePath { get; init; }

    public ushort? InternalEdidManufacturer { get; init; }

    public ushort? InternalEdidProduct { get; init; }

    public string? InternalExactName { get; init; }

    /// <summary>Preferência de resolução/Hz/escala da TV; nulos = automático.</summary>
    public int? TvWidth { get; init; }

    public int? TvHeight { get; init; }

    public int? TvRefreshHz { get; init; }

    public int? TvScalePercent { get; init; }
}

namespace CouchPad.Core.Display;

/// <summary>Preferência de modo da TV; null = automático (Windows escolhe).</summary>
public sealed record DisplayPreference(int? Width, int? Height, int? RefreshHz, int? ScalePercent)
{
    /// <summary>Escalas aceitas pelo Windows (%, inteiro).</summary>
    public static IReadOnlyList<int> SupportedScalePercents { get; } = [100, 125, 150, 175, 200];

    public static DisplayPreference Auto { get; } = new(null, null, null, null);

    public bool IsAuto => Width is null && Height is null && RefreshHz is null && ScalePercent is null;

    /// <summary>
    /// Verdadeiro quando cada componente informado é aceito: resolução completa
    /// (largura e altura juntas, ambas positivas), refresh positivo e escala na
    /// lista suportada.
    /// </summary>
    public bool IsValid =>
        (Width is null) == (Height is null) &&
        Width is null or > 0 &&
        Height is null or > 0 &&
        RefreshHz is null or > 0 &&
        (ScalePercent is not { } scale || SupportedScalePercents.Contains(scale));

    /// <summary>
    /// Descarta componentes inválidos (voltam para automático) sem lançar, para
    /// tolerar settings editados à mão ou gravados por versões antigas.
    /// </summary>
    public DisplayPreference Normalize()
    {
        var hasResolution = Width is > 0 && Height is > 0;
        return new DisplayPreference(
            hasResolution ? Width : null,
            hasResolution ? Height : null,
            RefreshHz is > 0 ? RefreshHz : null,
            ScalePercent is { } scale && SupportedScalePercents.Contains(scale) ? scale : null);
    }
}

/// <summary>Modo suportado pela TV (resolução + refresh).</summary>
public sealed record DisplayModeOption(int Width, int Height, int RefreshHz)
{
    /// <summary>Rótulo neutro de resolução (ex.: 1920×1080) para os seletores.</summary>
    public string ResolutionLabel => $"{Width}×{Height}";
}

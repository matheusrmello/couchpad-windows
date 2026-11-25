using CouchPad.Core.Display;

namespace CouchPad.App.ViewModels;

/// <summary>
/// Marca na lista de modos da TV o que o EDID não anuncia. A marcação é apenas
/// visual: o modo continua selecionável, aplicável e persistido normalmente.
/// </summary>
internal static class TvModeAdvertisement
{
    /// <summary>Tolerância de refresh (Hz) ao comparar EDID com o driver.</summary>
    internal const int RefreshToleranceHz = 1;

    /// <summary>Verdadeiro quando <paramref name="mode"/> está no conjunto anunciado.</summary>
    internal static bool IsAdvertised(
        DisplayModeOption mode,
        IReadOnlyList<DisplayModeOption> advertised)
    {
        ArgumentNullException.ThrowIfNull(advertised);
        return advertised.Any(candidate =>
            candidate.Width == mode.Width &&
            candidate.Height == mode.Height &&
            Math.Abs(candidate.RefreshHz - mode.RefreshHz) <= RefreshToleranceHz);
    }

    /// <summary>
    /// Uma linha por resolução, usando o primeiro modo da lista como
    /// representante (já ordenada por área e Hz desc). A resolução conta como
    /// anunciada quando qualquer taxa dela aparece no EDID. Sem EDID
    /// (lista vazia) nenhuma linha recebe marca.
    /// </summary>
    internal static IReadOnlyList<TvModeOption> BuildTvModeLabels(
        IReadOnlyList<DisplayModeOption> modes,
        IReadOnlyList<DisplayModeOption> advertised,
        string notAdvertisedMarker)
    {
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentNullException.ThrowIfNull(advertised);
        ArgumentNullException.ThrowIfNull(notAdvertisedMarker);

        var options = new List<TvModeOption>();
        foreach (var group in modes.GroupBy(mode => (mode.Width, mode.Height)))
        {
            var representative = group.First();
            var marked = advertised.Count > 0 && !group.Any(mode => IsAdvertised(mode, advertised));
            var label = marked
                ? $"{representative.ResolutionLabel} — {notAdvertisedMarker}"
                : representative.ResolutionLabel;
            options.Add(new TvModeOption(representative, label));
        }

        return options;
    }
}

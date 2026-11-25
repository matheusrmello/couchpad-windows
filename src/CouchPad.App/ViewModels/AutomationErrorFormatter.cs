using System.Globalization;
using CouchPad.App.Localization;
using CouchPad.Core.Display;

namespace CouchPad.App.ViewModels;

/// <summary>
/// Monta a linha de erro da automação. Puro: sem controle visual, sem hardware;
/// recebe apenas o que o coordenador registrou e devolve texto localizado.
/// </summary>
public static class AutomationErrorFormatter
{
    public static string? Format(
        LocalizationService localization,
        string? error,
        DisplayApplyErrorCode? code,
        DisplayModeOption? mode,
        int? nativeResult)
    {
        ArgumentNullException.ThrowIfNull(localization);
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        if (code is { } displayCode)
        {
            var formatted = displayCode switch
            {
                DisplayApplyErrorCode.UnsupportedMode when mode is { } unsupported =>
                    string.Format(localization[KeyFor(displayCode)],
                        unsupported.Width, unsupported.Height, unsupported.RefreshHz),
                DisplayApplyErrorCode.ModeRejected when mode is { } rejected =>
                    string.Format(localization[KeyFor(displayCode)],
                        rejected.Width, rejected.Height, rejected.RefreshHz,
                        (nativeResult ?? 0).ToString(CultureInfo.InvariantCulture)),
                DisplayApplyErrorCode.UnsupportedMode or DisplayApplyErrorCode.ModeRejected => null,
                _ => localization[KeyFor(displayCode)],
            };

            if (formatted is not null)
            {
                return formatted;
            }
        }

        return string.Format(localization["errors.automation"], error);
    }

    private static string KeyFor(DisplayApplyErrorCode code)
    {
        var name = code.ToString();
        return "errors.display." + char.ToLowerInvariant(name[0]) + name[1..];
    }
}

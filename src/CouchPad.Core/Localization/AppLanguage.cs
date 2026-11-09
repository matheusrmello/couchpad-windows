namespace CouchPad.Core.Localization;

public enum AppLanguage
{
    En = 0,
    PtBr = 1,
}

public static class AppLanguageExtensions
{
    /// <summary>D014: fallback é inglês.</summary>
    public const AppLanguage Default = AppLanguage.En;

    public static string ToCultureName(this AppLanguage language) => language switch
    {
        AppLanguage.PtBr => "pt-BR",
        _ => "en",
    };

    public static AppLanguage FromCultureName(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return Default;
        }

        var tag = cultureName.Trim();

        if (tag.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return AppLanguage.PtBr;
        }

        return tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.En
            : Default;
    }

    public static AppLanguage FromCurrentUICulture() =>
        FromCultureName(System.Globalization.CultureInfo.CurrentUICulture.Name);
}

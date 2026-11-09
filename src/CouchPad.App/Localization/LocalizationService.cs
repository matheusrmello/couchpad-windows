using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using CouchPad.Core.Localization;
using CouchPad.Core.Settings;

namespace CouchPad.App.Localization;

/// <summary>
/// Carrega os dicionários pt-BR/en e notifica a UI quando o idioma muda.
/// A persistência é opcional: falhas de escrita nunca derrubam a interface.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> LazyInstance =
        new(() => new LocalizationService());

    private readonly Dictionary<AppLanguage, IReadOnlyDictionary<string, string>> _catalogs = new();
    private IAppSettingsStore? _store;
    private AppLanguage _language = AppLanguageExtensions.Default;

    private LocalizationService()
    {
    }

    public static LocalizationService Instance => LazyInstance.Value;

    public AppLanguage Language => _language;

    /// <summary>Muda a cada troca de idioma; usada para reavaliar os bindings de tradução.</summary>
    public int Revision { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (_catalogs.TryGetValue(_language, out var current) &&
                current.TryGetValue(key, out var value))
            {
                return value;
            }

            if (_catalogs.TryGetValue(AppLanguage.En, out var fallback) &&
                fallback.TryGetValue(key, out var fallbackValue))
            {
                return fallbackValue;
            }

            return key;
        }
    }

    public void Initialize(IAppSettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        if (_catalogs.Count == 0)
        {
            _catalogs[AppLanguage.PtBr] = LoadCatalog("pt-BR");
            _catalogs[AppLanguage.En] = LoadCatalog("en");
        }

        var settings = _store.Load();
        var language = settings.Language is { Length: > 0 }
            ? AppLanguageExtensions.FromCultureName(settings.Language)
            : AppLanguageExtensions.FromCurrentUICulture();

        SetLanguage(language, persist: false);
    }

    public void SetLanguage(AppLanguage language) => SetLanguage(language, persist: true);

    private void SetLanguage(AppLanguage language, bool persist)
    {
        if (_language == language && persist)
        {
            return;
        }

        _language = language;
        Revision++;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Revision)));

        if (persist)
        {
            Persist(language);
        }
    }

    private void Persist(AppLanguage language)
    {
        if (_store is null)
        {
            return;
        }

        try
        {
            var settings = _store.Load();
            _store.Save(settings with { Language = language.ToCultureName() });
        }
        catch
        {
            // Preferência de idioma não é crítica: a UI continua funcionando.
        }
    }

    private static IReadOnlyDictionary<string, string> LoadCatalog(string culture)
    {
        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Localization",
                $"Strings.{culture}.json");

            if (!File.Exists(path))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return data is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(data, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}

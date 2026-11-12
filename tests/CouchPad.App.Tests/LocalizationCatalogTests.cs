using System.Text.Json;

namespace CouchPad.App.Tests;

public sealed class LocalizationCatalogTests
{
    [Fact]
    public void Portuguese_and_english_catalogs_have_matching_nonempty_entries()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Localization");
        var pt = Load(Path.Combine(directory, "Strings.pt-BR.json"));
        var en = Load(Path.Combine(directory, "Strings.en.json"));
        Assert.Equal(pt.Keys.Order(StringComparer.Ordinal), en.Keys.Order(StringComparer.Ordinal));
        Assert.All(pt, item => Assert.False(string.IsNullOrWhiteSpace(item.Value), item.Key));
        Assert.All(en, item => Assert.False(string.IsNullOrWhiteSpace(item.Value), item.Key));
    }

    private static Dictionary<string, string> Load(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Localization catalog is empty.");
}

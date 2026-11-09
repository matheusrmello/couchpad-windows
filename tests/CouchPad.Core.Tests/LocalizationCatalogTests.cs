using System.Text.Json;

namespace CouchPad.Core.Tests;

public sealed class LocalizationCatalogTests
{
    [Fact]
    public void Catalogs_have_same_keys_and_no_empty_values()
    {
        var (pt, en) = LoadCatalogs();

        Assert.Equal(pt.Keys.OrderBy(k => k), en.Keys.OrderBy(k => k));
        Assert.All(pt.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.All(en.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void Footer_version_keeps_placeholder_in_both_languages()
    {
        var (pt, en) = LoadCatalogs();

        Assert.Contains("{0}", pt["footer.version"]);
        Assert.Contains("{0}", en["footer.version"]);
    }

    private static (Dictionary<string, string> Pt, Dictionary<string, string> En) LoadCatalogs()
    {
        var root = FindRepositoryRoot();
        var localization = Path.Combine(root, "src", "CouchPad.App", "Localization");

        return (
            Load(Path.Combine(localization, "Strings.pt-BR.json")),
            Load(Path.Combine(localization, "Strings.en.json")));
    }

    private static Dictionary<string, string> Load(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
        ?? throw new InvalidOperationException($"Catálogo vazio: {path}");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CouchPad.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

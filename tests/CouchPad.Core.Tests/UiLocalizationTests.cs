using System.Text.Json;
using System.Text.RegularExpressions;

namespace CouchPad.Core.Tests;

/// <summary>
/// Garante que o XAML não tenha texto visível fora dos dois catálogos.
/// </summary>
public sealed class UiLocalizationTests
{
    private static readonly string[] SectionKeys =
    {
        "section.automation",
        "section.display",
        "section.gamepad",
        "section.combo",
        "section.settings",
        "section.transition",
        "section.launchers",
    };

    private static readonly string[] CardNames =
    {
        "AutomationCard",
        "DisplayCard",
        "GamepadCard",
        "ComboCard",
        "SettingsCard",
        "CountdownCard",
        "TransitionCard",
        "LaunchersCard",
    };

    private static readonly Regex TrKeyPattern =
        new(@"\{loc:Tr\s+([A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled);

    private static readonly Regex LiteralVisibleTextPattern = new(
        @"(?<=\s)(?:AutomationProperties\.(?:Name|HelpText)|ToolTip\.Tip|Text|Content|Title|Watermark|PlaceholderText|Header|Tag)=""([^""{][^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex InlineVisibleTextPattern = new(
        @">\s*([^<>{}\s][^<>{}]*?)\s*</(?:TextBlock|Button|ComboBoxItem)>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void Translation_keys_used_in_xaml_exist_in_both_catalogs()
    {
        var (pt, en) = LoadCatalogs();
        var keys = ReadXamlFiles()
            .SelectMany(file => TrKeyPattern.Matches(file.Value).Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            Assert.True(pt.ContainsKey(key), $"Chave ausente em pt-BR: {key}");
            Assert.True(en.ContainsKey(key), $"Chave ausente em inglês: {key}");
        }
    }

    [Fact]
    public void Xaml_has_no_hardcoded_visible_text()
    {
        foreach (var file in ReadXamlFiles())
        {
            foreach (Match match in LiteralVisibleTextPattern.Matches(file.Value))
            {
                Assert.Fail(
                    $"{Path.GetFileName(file.Key)}: texto visível hardcoded \"{match.Groups[1].Value}\". " +
                    "Use {loc:Tr chave} ou um binding para o ViewModel.");
            }

            foreach (Match match in InlineVisibleTextPattern.Matches(file.Value))
            {
                Assert.Fail(
                    $"{Path.GetFileName(file.Key)}: conteúdo inline hardcoded \"{match.Groups[1].Value}\". " +
                    "Use {loc:Tr chave}.");
            }
        }
    }

    [Fact]
    public void Main_window_references_all_sections_and_cards()
    {
        var path = Path.Combine(ViewsDirectory(), "MainWindow.axaml");
        var xaml = File.ReadAllText(path);

        var keys = TrKeyPattern.Matches(xaml)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in SectionKeys)
        {
            Assert.True(keys.Contains(key), $"Seção ausente no XAML: {key}");
        }

        foreach (var card in CardNames)
        {
            Assert.True(
                xaml.Contains($"x:Name=\"{card}\"", StringComparison.Ordinal),
                $"Card ausente no XAML: {card}");
        }
    }

    [Fact]
    public void Main_window_is_composed_from_shared_controls()
    {
        var xaml = File.ReadAllText(Path.Combine(ViewsDirectory(), "MainWindow.axaml"));

        foreach (var control in ComponentTypes.Where(control => control != "PathPicker"))
        {
            Assert.True(
                xaml.Contains($"controls:{control}", StringComparison.Ordinal),
                $"Componente ausente no shell: {control}");
        }
        Assert.Contains("{loc:Tr launchers.steam}", xaml);
        Assert.Contains("{loc:Tr launchers.hydra}", xaml);
    }

    [Fact]
    public void Windows_do_not_reintroduce_local_component_classes()
    {
        string[] windows = { "MainWindow.axaml", "AboutWindow.axaml", "ComponentGalleryWindow.axaml" };

        foreach (var window in windows)
        {
            var xaml = File.ReadAllText(Path.Combine(ViewsDirectory(), window));

            Assert.DoesNotContain("Classes=\"card\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Classes=\"pill", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Classes=\"keyChip\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Classes=\"pathBox\"", xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Component_themes_exist_for_every_control()
    {
        var xaml = File.ReadAllText(Path.Combine(AppDirectory(), "Themes", "Components.axaml"));

        foreach (var control in ComponentTypes)
        {
            Assert.True(
                xaml.Contains($"TargetType=\"controls:{control}\"", StringComparison.Ordinal),
                $"ControlTheme ausente: {control}");
        }
    }

    [Fact]
    public void Component_gallery_covers_every_state_with_localized_text()
    {
        var path = Path.Combine(ViewsDirectory(), "ComponentGalleryWindow.axaml");
        var xaml = File.ReadAllText(path);

        foreach (var control in ComponentTypes)
        {
            Assert.True(
                xaml.Contains($"controls:{control}", StringComparison.Ordinal),
                $"Componente ausente na galeria: {control}");
        }

        string[] states =
        {
            "State=\"Idle\"",
            "State=\"Loading\"",
            "State=\"Success\"",
            "State=\"Error\"",
            "Classes=\"loading\"",
            "Classes=\"error\"",
            "Classes=\"focus-demo\"",
            "Tone=\"Warning\"",
            "Tone=\"Danger\"",
            "IsEnabled=\"False\"",
        };

        foreach (var state in states)
        {
            Assert.True(
                xaml.Contains(state, StringComparison.Ordinal),
                $"Estado ausente na galeria: {state}");
        }
    }

    [Fact]
    public void Component_theme_has_no_hardcoded_visible_text()
    {
        var path = Path.Combine(AppDirectory(), "Themes", "Components.axaml");
        var content = File.ReadAllText(path);

        foreach (Match match in LiteralVisibleTextPattern.Matches(content))
        {
            Assert.Fail(
                $"Components.axaml: texto visível hardcoded \"{match.Groups[1].Value}\". " +
                "Use TemplateBinding ou {loc:Tr chave}.");
        }
    }

    [Fact]
    public void Dynamic_theme_resources_used_in_xaml_exist_in_tokens()
    {
        var tokens = File.ReadAllText(Path.Combine(AppDirectory(), "Themes", "Tokens.axaml"));
        var pattern = new Regex(@"\{DynamicResource\s+(CouchPad\.[A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled);
        var missing = Directory
            .EnumerateFiles(AppDirectory(), "*.axaml", SearchOption.AllDirectories)
            .SelectMany(path => pattern.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .Where(key => !tokens.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    private static readonly string[] ComponentTypes =
    {
        "Card",
        "StatusPill",
        "StatusRow",
        "KeyChip",
        "PathPicker",
        "ActionButton",
    };

    private static IEnumerable<KeyValuePair<string, string>> ReadXamlFiles() =>
        Directory
            .EnumerateFiles(ViewsDirectory(), "*.axaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new KeyValuePair<string, string>(path, File.ReadAllText(path)));

    private static string AppDirectory() =>
        Path.Combine(FindRepositoryRoot(), "src", "CouchPad.App");

    private static string ViewsDirectory() =>
        Path.Combine(AppDirectory(), "Views");

    private static (Dictionary<string, string> Pt, Dictionary<string, string> En) LoadCatalogs()
    {
        var localization = Path.Combine(FindRepositoryRoot(), "src", "CouchPad.App", "Localization");

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

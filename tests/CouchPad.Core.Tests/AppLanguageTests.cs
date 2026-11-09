using CouchPad.Core.Localization;

namespace CouchPad.Core.Tests;

public sealed class AppLanguageTests
{
    [Theory]
    [InlineData("pt-BR", AppLanguage.PtBr)]
    [InlineData("pt", AppLanguage.PtBr)]
    [InlineData("pt-PT", AppLanguage.PtBr)]
    [InlineData("en-US", AppLanguage.En)]
    [InlineData("en", AppLanguage.En)]
    [InlineData("fr-FR", AppLanguage.En)]
    [InlineData("", AppLanguage.En)]
    [InlineData("   ", AppLanguage.En)]
    [InlineData(null, AppLanguage.En)]
    public void FromCultureName_uses_english_fallback(string? cultureName, AppLanguage expected) =>
        Assert.Equal(expected, AppLanguageExtensions.FromCultureName(cultureName));

    [Theory]
    [InlineData(AppLanguage.PtBr, "pt-BR")]
    [InlineData(AppLanguage.En, "en")]
    public void ToCultureName_returns_supported_tag(AppLanguage language, string expected) =>
        Assert.Equal(expected, language.ToCultureName());

    [Fact]
    public void FromCultureName_is_case_insensitive() =>
        Assert.Equal(AppLanguage.PtBr, AppLanguageExtensions.FromCultureName("PT-br"));
}

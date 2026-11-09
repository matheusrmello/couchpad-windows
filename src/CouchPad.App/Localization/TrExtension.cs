using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace CouchPad.App.Localization;

/// <summary>
/// Uso em XAML: <c>Text="{loc:Tr automation.title}"</c>.
/// Retorna um binding que se atualiza quando o idioma muda.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding
        {
            Source = LocalizationService.Instance,
            Path = nameof(LocalizationService.Revision),
            Mode = BindingMode.OneWay,
            Converter = TrConverter.Instance,
            ConverterParameter = Key,
        };
}

public sealed class TrConverter : IValueConverter
{
    public static readonly TrConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        LocalizationService.Instance[parameter as string ?? string.Empty];

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

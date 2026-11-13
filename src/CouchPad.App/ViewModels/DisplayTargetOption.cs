namespace CouchPad.App.ViewModels;

/// <summary>Alvo de display descoberto pelo probe CCD (dados de hardware).</summary>
public sealed record DisplayTargetOption(
    string DevicePath,
    ushort? EdidManufacturer,
    ushort? EdidProduct,
    string? FriendlyName)
{
    public bool IsActive { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsHdmi { get; init; }

    public string DisplayName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FriendlyName) ? DevicePath : FriendlyName;
            var edid = EdidManufacturer is { } manufacturer && EdidProduct is { } product
                ? $"{manufacturer}/{product}"
                : "?";
            return $"{name} ({edid})";
        }
    }

    public bool Matches(DisplayTargetOption? other) =>
        other is not null &&
        string.Equals(DevicePath, other.DevicePath, StringComparison.OrdinalIgnoreCase) &&
        EdidManufacturer == other.EdidManufacturer &&
        EdidProduct == other.EdidProduct;
}

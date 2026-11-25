using CommunityToolkit.Mvvm.ComponentModel;
using CouchPad.Core.Settings;

namespace CouchPad.App.ViewModels;

/// <summary>Mutable UI representation of a named environment profile.</summary>
public partial class EnvironmentProfileOption : ObservableObject
{
    public EnvironmentProfileOption(string? id = null, string? name = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        Name = name ?? string.Empty;
    }

    public string Id { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    public string? TvAudioEndpointId { get; set; }

    public string? TvAudioEndpointName { get; set; }

    public string? TvDevicePath { get; set; }

    public ushort? TvEdidManufacturer { get; set; }

    public ushort? TvEdidProduct { get; set; }

    public string? TvExactName { get; set; }

    public string? InternalDevicePath { get; set; }

    public ushort? InternalEdidManufacturer { get; set; }

    public ushort? InternalEdidProduct { get; set; }

    public string? InternalExactName { get; set; }

    public int? TvWidth { get; set; }

    public int? TvHeight { get; set; }

    public int? TvRefreshHz { get; set; }

    public int? TvScalePercent { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id[..Math.Min(8, Id.Length)] : Name;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));

    public EnvironmentProfile ToModel() => new()
    {
        Id = Id,
        Name = Name,
        TvAudioEndpointId = TvAudioEndpointId,
        TvAudioEndpointName = TvAudioEndpointName,
        TvDevicePath = TvDevicePath,
        TvEdidManufacturer = TvEdidManufacturer,
        TvEdidProduct = TvEdidProduct,
        TvExactName = TvExactName,
        InternalDevicePath = InternalDevicePath,
        InternalEdidManufacturer = InternalEdidManufacturer,
        InternalEdidProduct = InternalEdidProduct,
        InternalExactName = InternalExactName,
        TvWidth = TvWidth,
        TvHeight = TvHeight,
        TvRefreshHz = TvRefreshHz,
        TvScalePercent = TvScalePercent,
    };

    public static EnvironmentProfileOption FromModel(EnvironmentProfile profile) =>
        new(profile.Id, profile.Name)
        {
            TvAudioEndpointId = profile.TvAudioEndpointId,
            TvAudioEndpointName = profile.TvAudioEndpointName,
            TvDevicePath = profile.TvDevicePath,
            TvEdidManufacturer = profile.TvEdidManufacturer,
            TvEdidProduct = profile.TvEdidProduct,
            TvExactName = profile.TvExactName,
            InternalDevicePath = profile.InternalDevicePath,
            InternalEdidManufacturer = profile.InternalEdidManufacturer,
            InternalEdidProduct = profile.InternalEdidProduct,
            InternalExactName = profile.InternalExactName,
            TvWidth = profile.TvWidth,
            TvHeight = profile.TvHeight,
            TvRefreshHz = profile.TvRefreshHz,
            TvScalePercent = profile.TvScalePercent,
        };
}

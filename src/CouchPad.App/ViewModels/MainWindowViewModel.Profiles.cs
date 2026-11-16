using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CouchPad.Core.Settings;

namespace CouchPad.App.ViewModels;

public partial class MainWindowViewModel
{
    public ObservableCollection<EnvironmentProfileOption> EnvironmentProfiles { get; } = new();

    [ObservableProperty]
    private EnvironmentProfileOption? _selectedEnvironmentProfile;

    public string SelectedProfileName
    {
        get => SelectedEnvironmentProfile?.Name ?? string.Empty;
        set
        {
            if (_isLoadingSettings || SelectedEnvironmentProfile is null ||
                string.Equals(SelectedEnvironmentProfile.Name, value, StringComparison.Ordinal))
                return;
            SelectedEnvironmentProfile.Name = value;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool CanDeleteEnvironmentProfile => EnvironmentProfiles.Count > 1;

    private void LoadEnvironmentProfiles(AppSettings settings)
    {
        EnvironmentProfiles.Clear();
        var configured = settings.EnvironmentProfiles ?? [];
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var model in configured)
        {
            index++;
            var id = string.IsNullOrWhiteSpace(model.Id) || !usedIds.Add(model.Id)
                ? Guid.NewGuid().ToString("N")
                : model.Id;
            usedIds.Add(id);
            var name = string.IsNullOrWhiteSpace(model.Name)
                ? string.Format(_localization["profiles.new_name"], index)
                : model.Name;
            EnvironmentProfiles.Add(EnvironmentProfileOption.FromModel(model with { Id = id, Name = name }));
        }

        if (EnvironmentProfiles.Count == 0)
        {
            EnvironmentProfiles.Add(new EnvironmentProfileOption(name: _localization["profiles.default_name"])
            {
                TvAudioEndpointId = settings.TvAudioEndpointId,
                TvDevicePath = settings.TvDevicePath,
                TvEdidManufacturer = settings.TvEdidManufacturer,
                TvEdidProduct = settings.TvEdidProduct,
                TvExactName = settings.TvExactName,
                InternalDevicePath = settings.InternalDevicePath,
                InternalEdidManufacturer = settings.InternalEdidManufacturer,
                InternalEdidProduct = settings.InternalEdidProduct,
                InternalExactName = settings.InternalExactName,
            });
        }

        NotifyProfileListChanged();
        var selected = EnvironmentProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, settings.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
            ?? EnvironmentProfiles[0];
        SelectedEnvironmentProfile = selected;
        ApplyProfileToSelection(selected);
    }

    private void ApplyProfileToSelection(EnvironmentProfileOption profile)
    {
        SelectedTvTarget = EnsureTarget(CreateSavedTarget(
            profile.TvDevicePath, profile.TvEdidManufacturer, profile.TvEdidProduct, profile.TvExactName));
        SelectedInternalTarget = EnsureTarget(CreateSavedTarget(
            profile.InternalDevicePath, profile.InternalEdidManufacturer, profile.InternalEdidProduct,
            profile.InternalExactName));
        SelectedTvAudioEndpoint = ResolveAudioEndpoint(profile.TvAudioEndpointId);
    }

    private AudioEndpointOption? ResolveAudioEndpoint(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var match = AudioEndpoints.FirstOrDefault(endpoint =>
            string.Equals(endpoint.Id, id, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        var placeholder = new AudioEndpointOption(id, null);
        AudioEndpoints.Add(placeholder);
        return placeholder;
    }

    private void SyncSelectedProfileFromSelection()
    {
        if (SelectedEnvironmentProfile is not { } profile)
        {
            return;
        }

        profile.TvAudioEndpointId = SelectedTvAudioEndpoint?.Id;
        profile.TvDevicePath = SelectedTvTarget?.DevicePath;
        profile.TvEdidManufacturer = SelectedTvTarget?.EdidManufacturer;
        profile.TvEdidProduct = SelectedTvTarget?.EdidProduct;
        profile.TvExactName = SelectedTvTarget?.FriendlyName;
        profile.InternalDevicePath = SelectedInternalTarget?.DevicePath;
        profile.InternalEdidManufacturer = SelectedInternalTarget?.EdidManufacturer;
        profile.InternalEdidProduct = SelectedInternalTarget?.EdidProduct;
        profile.InternalExactName = SelectedInternalTarget?.FriendlyName;
    }

    [RelayCommand]
    private void NewEnvironmentProfile()
    {
        var profile = new EnvironmentProfileOption(name: NextProfileName())
        {
            TvAudioEndpointId = SelectedTvAudioEndpoint?.Id,
            TvDevicePath = SelectedTvTarget?.DevicePath,
            TvEdidManufacturer = SelectedTvTarget?.EdidManufacturer,
            TvEdidProduct = SelectedTvTarget?.EdidProduct,
            TvExactName = SelectedTvTarget?.FriendlyName,
            InternalDevicePath = SelectedInternalTarget?.DevicePath,
            InternalEdidManufacturer = SelectedInternalTarget?.EdidManufacturer,
            InternalEdidProduct = SelectedInternalTarget?.EdidProduct,
            InternalExactName = SelectedInternalTarget?.FriendlyName,
        };
        EnvironmentProfiles.Add(profile);
        NotifyProfileListChanged();
        SelectedEnvironmentProfile = profile;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteEnvironmentProfile))]
    private void DeleteEnvironmentProfile()
    {
        if (SelectedEnvironmentProfile is not { } profile || EnvironmentProfiles.Count <= 1)
        {
            return;
        }

        var index = EnvironmentProfiles.IndexOf(profile);
        EnvironmentProfiles.Remove(profile);
        NotifyProfileListChanged();
        SelectedEnvironmentProfile = EnvironmentProfiles[Math.Min(index, EnvironmentProfiles.Count - 1)];
    }

    private string NextProfileName()
    {
        var index = EnvironmentProfiles.Count + 1;
        string name;
        do
        {
            name = string.Format(_localization["profiles.new_name"], index++);
        }
        while (EnvironmentProfiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)));
        return name;
    }

    private void NotifyProfileListChanged()
    {
        OnPropertyChanged(nameof(CanDeleteEnvironmentProfile));
        DeleteEnvironmentProfileCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedEnvironmentProfileChanged(EnvironmentProfileOption? value)
    {
        OnPropertyChanged(nameof(SelectedProfileName));
        if (value is null || _isLoadingSettings)
        {
            return;
        }

        _isLoadingSettings = true;
        try
        {
            ApplyProfileToSelection(value);
        }
        finally
        {
            _isLoadingSettings = false;
        }

        OnHardwareSelectionChanged();
    }
}

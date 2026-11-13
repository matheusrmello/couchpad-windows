using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CouchPad.App.Updates;

namespace CouchPad.App.ViewModels;

public partial class MainWindowViewModel
{
    private GitHubUpdateCheck? _updateCheck;
    private CancellationTokenSource? _updateRequest;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateNoticeText))]
    [NotifyPropertyChangedFor(nameof(HasUpdateNotice))]
    private UpdateNotice? _availableUpdate;

    [ObservableProperty]
    private bool _checkForUpdates = true;

    [ObservableProperty]
    private bool _includePreviewUpdates;

    public bool UpdatesAvailable => _updateCheck is not null;
    public bool HasUpdateNotice => AvailableUpdate is not null;

    public string? UpdateNoticeText => AvailableUpdate is { } update
        ? string.Format(_localization[update.IsPreview ? "update.preview" : "update.available"], update.Version)
        : null;

    internal void StartUpdateChecks(GitHubUpdateCheck check)
    {
        _updateCheck = check;
        OnPropertyChanged(nameof(UpdatesAvailable));
        RefreshUpdateNotice();
    }

    internal void StopUpdateChecks()
    {
        _updateCheck = null;
        _updateRequest?.Cancel();
    }

    partial void OnCheckForUpdatesChanged(bool value) => OnUpdatePreferencesChanged();
    partial void OnIncludePreviewUpdatesChanged(bool value) => OnUpdatePreferencesChanged();

    private void OnUpdatePreferencesChanged()
    {
        if (_isLoadingSettings) return;
        PersistSettings();
        RefreshUpdateNotice();
    }

    private async void RefreshUpdateNotice()
    {
        _updateRequest?.Cancel();
        AvailableUpdate = null;
        if (!CheckForUpdates || _updateCheck is not { } check) return;
        using var request = new CancellationTokenSource();
        var includePreviews = IncludePreviewUpdates;
        _updateRequest = request;
        try
        {
            var update = await Task.Run(() => check.CheckAsync(
                GitHubUpdateCheck.InstalledVersion, includePreviews, request.Token));
            if (!request.IsCancellationRequested) AvailableUpdate = update;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_updateRequest, request)) _updateRequest = null;
        }
    }

    [RelayCommand]
    private void OpenUpdate()
    {
        if (AvailableUpdate is not { } update) return;
        try { Process.Start(new ProcessStartInfo(update.ReleasePage.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}

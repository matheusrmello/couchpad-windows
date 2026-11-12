using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.App.Views;
using CouchPad.Core.Automation;
using CouchPad.Core.Audio;
using CouchPad.Core.Display;
using CouchPad.Core.Localization;
using CouchPad.Core.Settings;
using CouchPad.Core.Storage;
using CouchPad.Platform.Windows;

namespace CouchPad.App;

public partial class App : Application
{
    // Held until process exit, including shutdown while a watchdog is active.
    private DisplaySessionOwner? _displaySessionOwner;
    private WindowsGamepadWatcher? _gamepadWatcher;
    private WindowsCountdownNotifications? _countdownNotifications;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var isSmoke = desktop.Args?.Contains("--smoke", StringComparer.Ordinal) == true;
            var isGallery = desktop.Args?.Contains("--gallery", StringComparer.Ordinal) == true;
            var settingsPath = isSmoke
                ? Path.Combine(Path.GetTempPath(), "couchpad-smoke", "settings.json")
                : AppPaths.SettingsFile();
            var journalPath = isSmoke
                ? Path.Combine(Path.GetTempPath(), "couchpad-smoke", "state", "session.json")
                : AppPaths.SessionJournalFile();

            var settingsStore = new JsonAppSettingsStore(settingsPath);
            LocalizationService.Instance.Initialize(settingsStore);
            var appSettings = settingsStore.Load();
            var useWindowsGamepad = !isSmoke && !isGallery && OperatingSystem.IsWindows() &&
                (appSettings.UseWindowsGamepad || desktop.Args?.Contains("--windows-gamepad", StringComparer.Ordinal) == true);

            var transaction = new RescueTransactionContext(Path.GetDirectoryName(Path.GetFullPath(journalPath))!);
            var (displayService, rescueGuard) = CreateWindowsDisplayServices(
                desktop.Args ?? [], journalPath, isSmoke || isGallery,
                Path.Combine(AppContext.BaseDirectory, "CouchPad.Rescue.exe"), appSettings, transaction);
            var (audioService, audioGuard) = CreateWindowsAudioServices(displayService, appSettings, transaction,
                Path.Combine(AppContext.BaseDirectory, "CouchPad.Rescue.exe"), journalPath);
            if (audioGuard is not null) rescueGuard = audioGuard;
            _displaySessionOwner = isGallery ? null : AcquireDisplayOwner(journalPath);
            if (useWindowsGamepad) _gamepadWatcher = new WindowsGamepadWatcher();
            if (!isSmoke && !isGallery && OperatingSystem.IsWindows() &&
                desktop.Args?.Contains("--experimental-windows-notifications", StringComparer.Ordinal) == true)
                _countdownNotifications = new WindowsCountdownNotifications(Path.Combine(AppContext.BaseDirectory, "notifications", "CouchPad.Notifications.Windows.exe"));
            desktop.Exit += (_, _) => { _gamepadWatcher?.Dispose(); _countdownNotifications?.Dispose(); };
            Func<IReadOnlyList<DisplayTargetOption>>? discoverTargets =
                isSmoke || isGallery || !OperatingSystem.IsWindows()
                    ? null
                    : DiscoverDisplayTargets;

            Window window = isGallery
                ? new ComponentGalleryWindow()
                : new MainWindow
                {
                    DataContext = new MainWindowViewModel(
                        LocalizationService.Instance,
                        settingsStore,
                        new JsonSessionJournalStore(journalPath),
                        gamepadWatcher: _gamepadWatcher,
                        displayService: displayService,
                        audioService: audioService,
                        rescueGuard: rescueGuard,
                        discoverTargets: discoverTargets,
                        discoverAudioEndpoints: isSmoke || isGallery || !OperatingSystem.IsWindows() ? null : DiscoverAudioEndpoints,
                        revokeAudioWrites: OperatingSystem.IsWindows() && audioService is WindowsAudioService windowsAudio
                            ? windowsAudio.DisableNewWrites : null,
                        showCountdown: _countdownNotifications is { } notifications ? notifications.Show : null,
                        dismissCountdown: _countdownNotifications is { } notificationsToDismiss ? notificationsToDismiss.Cancel : null,
                        launcherService: displayService is WindowsDisplayService && appSettings.AllowDisplayWrites
                            ? new WindowsLauncherService() : null),
                };

            desktop.MainWindow = window;

            if (isSmoke)
            {
                window.Opened += async (_, _) =>
                {
                    await Task.Delay(600);

                    var localization = LocalizationService.Instance;
                    var initial = localization.Language;

                    localization.SetLanguage(AppLanguage.PtBr);
                    var pt = localization["automation.toggle"];
                    localization.SetLanguage(AppLanguage.En);
                    var en = localization["automation.toggle"];

                    Console.WriteLine(
                        $"SMOKE: window opened {window.Bounds.Width}x{window.Bounds.Height}; " +
                        $"initial={initial}; pt='{pt}'; en='{en}'; " +
                        $"language={localization.Language}; title='{window.Title}'");

                    window.Close();
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static DisplaySessionOwner? AcquireDisplayOwner(string journalPath) =>
        OperatingSystem.IsWindows() ? DisplaySessionOwner.Acquire(journalPath) : null;

    internal static (IDisplayService? Display, IRescueGuard? Guard) CreateWindowsDisplayServices(
        string[] args, string journalPath, bool simulationOnly, string rescuePath,
        AppSettings? settings = null, RescueTransactionContext? sharedTransaction = null)
    {
        if (simulationOnly) return (null, null);
        var allowWrites = settings?.AllowDisplayWrites == true;
        const string flag = "--windows-display";
        var positions = args.Select((value, index) => (value, index))
            .Where(item => string.Equals(item.value, flag, StringComparison.Ordinal)).ToArray();
        if (!OperatingSystem.IsWindows()) return (null, null);
        if (positions.Length == 0 && !allowWrites) return (null, null);
        if (positions.Length > 1) throw new ArgumentException("Invalid Windows display configuration.");

        var tvPath = settings?.TvDevicePath;
        var tvManufacturer = settings?.TvEdidManufacturer;
        var tvProduct = settings?.TvEdidProduct;
        var internalPath = settings?.InternalDevicePath;
        var internalManufacturer = settings?.InternalEdidManufacturer;
        var internalProduct = settings?.InternalEdidProduct;

        if (!IsComplete(tvPath, tvManufacturer, tvProduct) ||
            !IsComplete(internalPath, internalManufacturer, internalProduct))
        {
            if (positions.Length == 0) return (null, null);
            if (positions[0].index + 6 >= args.Length)
                throw new ArgumentException("Invalid Windows display configuration.");

            var i = positions[0].index;
            tvPath = args[i + 1];
            internalPath = args[i + 4];
            if (string.IsNullOrWhiteSpace(tvPath) || string.IsNullOrWhiteSpace(internalPath) ||
                !ushort.TryParse(args[i + 2], out var tvEdidManufacturer) ||
                !ushort.TryParse(args[i + 3], out var tvEdidProduct) ||
                !ushort.TryParse(args[i + 5], out var internalEdidManufacturer) ||
                !ushort.TryParse(args[i + 6], out var internalEdidProduct))
            {
                throw new ArgumentException("Invalid Windows display selector.");
            }

            tvManufacturer = tvEdidManufacturer;
            tvProduct = tvEdidProduct;
            internalManufacturer = internalEdidManufacturer;
            internalProduct = internalEdidProduct;
        }

        if (!IsComplete(tvPath, tvManufacturer, tvProduct) ||
            !IsComplete(internalPath, internalManufacturer, internalProduct))
        {
            return (null, null);
        }

        if (!File.Exists(rescuePath)) throw new FileNotFoundException("Rescue executable is unavailable.", rescuePath);
        var options = new WindowsDisplayOptions(
            tvPath!, tvManufacturer!.Value, tvProduct!.Value, settings?.TvExactName,
            AllowWrites: allowWrites,
            InternalDisplay: new DisplaySelector(
                internalPath!, internalManufacturer!.Value, internalProduct!.Value,
                settings?.InternalExactName));
        var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(journalPath))!;
        var transaction = sharedTransaction ?? new RescueTransactionContext(stateDirectory);
        return (new WindowsDisplayService(options, transaction),
            new WindowsRescueGuard(rescuePath, journalPath, TimeSpan.FromSeconds(60),
                allowRecovery: allowWrites, transaction: transaction));
    }

    internal static (IAudioService? Audio, IRescueGuard? Guard) CreateWindowsAudioServices(
        IDisplayService? display, AppSettings settings, RescueTransactionContext transaction, string rescuePath, string journalPath,
        string? hostPath = null)
    {
        if (!OperatingSystem.IsWindows() || display is not WindowsDisplayService || !settings.AllowDisplayWrites) return (null, null);
        var expectedHost = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rescuePath))!, "CouchPad.exe"));
        var actualHost = hostPath ?? Environment.ProcessPath;
        if (!settings.AllowAudioWrites || string.IsNullOrWhiteSpace(settings.TvAudioEndpointId) || string.IsNullOrEmpty(actualHost) ||
            !string.Equals(Path.GetFullPath(actualHost), expectedHost, StringComparison.OrdinalIgnoreCase)) return (new UnavailableAudioService(), null);
        return (new WindowsAudioService(new WindowsAudioOptions(settings.TvAudioEndpointId, AllowWrites: true), transaction),
            new WindowsAudioRescueGuard(rescuePath, journalPath, TimeSpan.FromSeconds(60), transaction, allowRecovery: true));
    }

    internal static IReadOnlyList<AudioEndpointOption> DiscoverAudioEndpoints()
    {
        if (!OperatingSystem.IsWindows()) return [];
        try { return WindowsAudioProbe.Capture().Endpoints.Select(e => new AudioEndpointOption(e.Id, e.FriendlyName)).ToArray(); }
        catch { return []; }
    }

    private sealed class UnavailableAudioService : IAudioService
    {
        public AudioOutput GetCurrent() => AudioOutput.Default;
        public Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return mode == AudioMode.KeepCurrent ? Task.FromResult(AudioOutput.Default) :
                throw new InvalidOperationException("Windows audio is not configured.");
        }
        public Task RestoreAsync(AudioOutput state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal static IReadOnlyList<DisplayTargetOption> DiscoverDisplayTargets()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<DisplayTargetOption>();

        try
        {
            var service = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
            var snapshot = service.Capture(WindowsDisplayService.AllPaths);
            return snapshot.Targets
                .Where(target => target.Status == 0 && !string.IsNullOrWhiteSpace(target.DevicePath))
                .DistinctBy(target => (target.AdapterLow, target.AdapterHigh, target.Id))
                .Select(target => new DisplayTargetOption(
                    target.DevicePath!,
                    target.EdidManufacturer,
                    target.EdidProduct,
                    target.FriendlyName))
                .OrderBy(target => target.FriendlyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(target => target.DevicePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<DisplayTargetOption>();
        }
    }

    private static bool IsComplete(string? path, ushort? manufacturer, ushort? product) =>
        !string.IsNullOrWhiteSpace(path) && manufacturer.HasValue && product.HasValue;
}

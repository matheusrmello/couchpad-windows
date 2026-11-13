using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using CouchPad.App.Localization;
using CouchPad.App.Updates;
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
            var isSimulation = isSmoke || desktop.Args?.Contains("--simulate", StringComparer.Ordinal) == true;
            var isProduction = !isSimulation && !isGallery && OperatingSystem.IsWindows();
            var simulationRoot = Path.Combine(Path.GetTempPath(), isSmoke ? "couchpad-smoke" : "couchpad-simulation");
            var settingsPath = isSimulation
                ? AppPaths.SettingsFile(simulationRoot)
                : AppPaths.SettingsFile();
            var journalPath = isSimulation
                ? AppPaths.SessionJournalFile(simulationRoot)
                : AppPaths.SessionJournalFile();

            var settingsStore = new JsonAppSettingsStore(settingsPath);
            LocalizationService.Instance.Initialize(settingsStore);
            var appSettings = settingsStore.Load();
            var rescuePath = Path.Combine(AppContext.BaseDirectory, "CouchPad.Rescue.exe");
            var appPath = Path.Combine(AppContext.BaseDirectory, "CouchPad.exe");
            var canUseRescue = isProduction && File.Exists(rescuePath);
            var startup = canUseRescue && File.Exists(appPath) ? new WindowsScheduledStartup() : null;
            var hardwareSettings = canUseRescue && HasProductionSelectors(appSettings)
                ? appSettings with { AllowDisplayWrites = true, AllowAudioWrites = true }
                : appSettings with { AllowDisplayWrites = false, AllowAudioWrites = false };

            var transaction = new RescueTransactionContext(Path.GetDirectoryName(Path.GetFullPath(journalPath))!);
            var (displayService, rescueGuard) = CreateWindowsDisplayServices(
                [], journalPath, !isProduction,
                rescuePath, hardwareSettings, transaction);
            var (audioService, audioGuard) = CreateWindowsAudioServices(displayService, hardwareSettings, transaction,
                rescuePath, journalPath);
            if (audioGuard is not null) rescueGuard = audioGuard;
            _displaySessionOwner = isGallery || Program.OwnsDisplaySession ? null : AcquireDisplayOwner(journalPath);
            if (isProduction) _gamepadWatcher = new WindowsGamepadWatcher();
            if (isProduction &&
                desktop.Args?.Contains("--experimental-windows-notifications", StringComparer.Ordinal) == true)
                _countdownNotifications = new WindowsCountdownNotifications(Path.Combine(AppContext.BaseDirectory, "notifications", "CouchPad.Notifications.Windows.exe"));
            desktop.Exit += (_, _) => { _gamepadWatcher?.Dispose(); _countdownNotifications?.Dispose(); };
            Func<IReadOnlyList<DisplayTargetOption>>? discoverTargets =
                !isProduction
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
                        discoverAudioEndpoints: isProduction ? DiscoverAudioEndpoints : null,
                        revokeAudioWrites: OperatingSystem.IsWindows() && audioService is WindowsAudioService windowsAudio
                            ? windowsAudio.DisableNewWrites : null,
                        showCountdown: _countdownNotifications is { } notifications ? notifications.Show : null,
                        dismissCountdown: _countdownNotifications is { } notificationsToDismiss ? notificationsToDismiss.Cancel : null,
                        launcherService: audioGuard is not null
                            ? new WindowsLauncherService() : null,
                        readStartup: startup is null ? null : () => startup.ReadAndMigrate(appPath),
                        configureStartup: startup is null ? null : enabled => startup.SetEnabled(enabled, appPath),
                        suspendAutomation: desktop.Args?.Any(arg => arg is "--recovery-succeeded" or "--recovery-failed" or "--hardware-applied") == true,
                        canStartRecovery: canUseRescue,
                        recoveryFailed: desktop.Args?.Contains("--recovery-failed") == true,
                        productionMode: isProduction,
                        configuredHardware: audioGuard is not null ? hardwareSettings : null,
                        restartForHardware: canUseRescue && Program.CanRestartForHardware ? Program.StartHardwareRestart : null,
                        playAudioTest: isProduction ? WindowsAudioTestTone.PlayOnEndpoint : null,
                        hardwareApplied: desktop.Args?.Contains("--hardware-applied", StringComparer.Ordinal) == true),
                };

            desktop.MainWindow = window;

            if (isProduction && window.DataContext is MainWindowViewModel viewModel)
            {
                var updateClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                {
                    Timeout = TimeSpan.FromSeconds(8),
                    MaxResponseContentBufferSize = 4 * 1024 * 1024,
                };
                var updateCheck = new GitHubUpdateCheck(updateClient, Path.Combine(AppPaths.GetRoot(), "update-cache.json"), TimeProvider.System);
                void StartUpdateCheck(object? sender, EventArgs e)
                {
                    window.Opened -= StartUpdateCheck;
                    viewModel.StartUpdateChecks(updateCheck);
                }
                window.Opened += StartUpdateCheck;
                desktop.Exit += (_, _) =>
                {
                    viewModel.StopUpdateChecks();
                    updateClient.Dispose();
                };
                var open = new NativeMenuItem();
                var automation = new NativeMenuItem();
                var exit = new NativeMenuItem();
                void UpdateTrayText()
                {
                    open.Header = LocalizationService.Instance["tray.open"];
                    automation.Header = LocalizationService.Instance[viewModel.AutomationEnabled
                        ? "tray.disable" : "tray.enable"];
                    automation.IsEnabled = viewModel.CanToggleAutomation;
                    exit.Header = LocalizationService.Instance["tray.exit"];
                }
                viewModel.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(MainWindowViewModel.AutomationEnabled) or nameof(MainWindowViewModel.CanToggleAutomation))
                        UpdateTrayText();
                };
                void ShowWindow()
                {
                    window.Show();
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                var allowExit = false;
                var exiting = false;
                viewModel.RecoveryRequested += (_, _) =>
                {
                    if (exiting) return;
                    try
                    {
                        WindowsAssistedRecovery.Start(rescuePath);
                    }
                    catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
                    {
                        viewModel.RecoveryFailed = true;
                        return;
                    }
                    exiting = true;
                    allowExit = true;
                    desktop.Shutdown();
                };
                async void ExitSafely()
                {
                    if (exiting) return;
                    exiting = true;
                    try
                    {
                        if (await viewModel.PrepareExitAsync())
                        {
                            allowExit = true;
                            desktop.Shutdown();
                        }
                        else ShowWindow();
                    }
                    catch (Exception) { ShowWindow(); }
                    finally { exiting = false; }
                }
                viewModel.HardwareRestartStarted += (_, _) =>
                {
                    exiting = true;
                    allowExit = true;
                    desktop.Shutdown();
                };
                open.Click += (_, _) => ShowWindow();
                Program.Instance?.Listen(async token =>
                    await Dispatcher.UIThread.InvokeAsync(ShowWindow, DispatcherPriority.Normal, token));
                automation.Click += (_, _) =>
                {
                    if (viewModel.CanToggleAutomation)
                        viewModel.AutomationEnabled = !viewModel.AutomationEnabled;
                };
                exit.Click += (_, _) => ExitSafely();
                var menu = new NativeMenu();
                menu.Items.Add(open);
                menu.Items.Add(automation);
                menu.Items.Add(exit);
                using var stream = AssetLoader.Open(new Uri("avares://CouchPad/Assets/couchpad.ico"));
                var trayIcon = new TrayIcon
                {
                    Icon = new WindowIcon(stream),
                    ToolTipText = LocalizationService.Instance["app.name"],
                    Menu = menu,
                    IsVisible = true,
                };
                trayIcon.Clicked += (_, _) => ShowWindow();
                TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
                UpdateTrayText();
                LocalizationService.Instance.PropertyChanged += (_, _) =>
                {
                    UpdateTrayText();
                    trayIcon.ToolTipText = LocalizationService.Instance["app.name"];
                };
                window.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Window.WindowStateProperty && window.WindowState == WindowState.Minimized && viewModel.MinimizeToTray)
                        window.Hide();
                };
                window.Closing += (_, e) =>
                {
                    if (allowExit) return;
                    e.Cancel = true;
                    if (viewModel.MinimizeToTray) window.Hide();
                    else ExitSafely();
                };
            }

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

    internal static bool HasProductionSelectors(AppSettings settings) =>
        IsComplete(settings.TvDevicePath, settings.TvEdidManufacturer, settings.TvEdidProduct) &&
        IsComplete(settings.InternalDevicePath, settings.InternalEdidManufacturer, settings.InternalEdidProduct) &&
        !string.Equals(settings.TvDevicePath, settings.InternalDevicePath, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(settings.TvAudioEndpointId);

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
        var inventory = WindowsAudioProbe.Capture();
        return inventory.Endpoints.Select(e => new AudioEndpointOption(e.Id, e.FriendlyName, e.State == 1,
            string.Equals(e.Id, inventory.Multimedia, StringComparison.OrdinalIgnoreCase),
            CanAppearAfterDisplayChange: e.State is 4 or 8 && e.IsDisplayAudio, IsDisplayAudio: e.IsDisplayAudio)).ToArray();
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

        var service = new WindowsDisplayService(new WindowsDisplayOptions("", 0, 0));
        var snapshot = service.Capture(WindowsDisplayService.AllPaths);
        return snapshot.Targets
            .Where(target => target.Status == 0 && !string.IsNullOrWhiteSpace(target.DevicePath))
            .GroupBy(target => (target.AdapterLow, target.AdapterHigh, target.Id))
            .Select(group =>
            {
                var target = group.First();
                return new DisplayTargetOption(target.DevicePath!, target.EdidManufacturer, target.EdidProduct,
                    target.FriendlyName)
                {
                    IsActive = group.Any(path => path.IsActive),
                    IsAvailable = group.Any(path => path.IsAvailable),
                    IsHdmi = group.Any(path => path.IsAvailable && path.IsHdmi),
                };
            })
            .OrderBy(target => target.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.DevicePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsComplete(string? path, ushort? manufacturer, ushort? product) =>
        !string.IsNullOrWhiteSpace(path) && manufacturer.HasValue && product.HasValue;
}

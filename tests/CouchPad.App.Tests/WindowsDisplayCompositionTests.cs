using System.Reflection;
using CouchPad.App;
using CouchPad.Core.Settings;
using CouchPad.Platform.Windows;

namespace CouchPad.App.Tests;

public class WindowsDisplayCompositionTests
{
    private static readonly string[] Arguments = ["--windows-display", "tv-path", "12", "34", "monitor-path", "56", "78"];

    [Fact]
    public async Task Real_audio_requires_opt_in_and_a_selected_endpoint()
    {
        if (!OperatingSystem.IsWindows()) return;
        var display = new WindowsDisplayService(new WindowsDisplayOptions("tv", 1, 2));
        var context = new RescueTransactionContext(Path.GetTempPath());
        var settings = new AppSettings { AllowDisplayWrites = true };
        var (blocked, noGuard) = App.CreateWindowsAudioServices(display, settings, context, "unused", "journal");
        Assert.NotNull(blocked);
        Assert.Null(noGuard);
        await Assert.ThrowsAsync<InvalidOperationException>(() => blocked.ApplyAsync(CouchPad.Core.Audio.AudioMode.MoveToHdmi));
        var (audio, guard) = App.CreateWindowsAudioServices(display,
            settings with { AllowAudioWrites = true, TvAudioEndpointId = "tv-id" }, context, "unused", "journal");
        Assert.IsType<WindowsAudioService>(audio);
        Assert.IsType<WindowsAudioRescueGuard>(guard);
        Assert.Equal((null, null), App.CreateWindowsAudioServices(null, settings, context, "unused", "journal"));
    }

    [Fact]
    public void Real_display_session_is_exclusive_before_the_view_model_is_created()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "couchpad-app-owner-" + Guid.NewGuid().ToString("N"));
        var journal = Path.Combine(directory, "session.json");
        try
        {
            using (App.AcquireDisplayOwner(journal))
                Assert.Throws<IOException>(() => App.AcquireDisplayOwner(journal));
            using var next = App.AcquireDisplayOwner(journal);
            Assert.NotNull(next);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Defaults_and_smoke_use_simulation()
    {
        Assert.Equal((null, null), App.CreateWindowsDisplayServices([], "journal", false, "missing"));
        Assert.Equal((null, null), App.CreateWindowsDisplayServices(Arguments, "journal", true, "missing"));
    }

    [Fact]
    public void Windows_service_and_guard_are_created_together_with_writes_disabled()
    {
        if (!OperatingSystem.IsWindows()) return;
        var rescue = Path.GetTempFileName();
        try
        {
            var journal = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "couchpad-composition-journal.json"));
            var (display, guard) = App.CreateWindowsDisplayServices(Arguments, journal, false, rescue);
            var windowsDisplay = Assert.IsType<WindowsDisplayService>(display);
            var windowsGuard = Assert.IsType<WindowsRescueGuard>(guard);
            var options = (WindowsDisplayOptions)typeof(WindowsDisplayService).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(windowsDisplay)!;
            Assert.False(options.AllowWrites);
            Assert.Equal("tv-path", options.DevicePath);
            Assert.Equal("monitor-path", options.InternalDisplay!.DevicePath);
            Assert.Equal(journal, typeof(WindowsRescueGuard).GetField("_journal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(windowsGuard));
            Assert.False((bool)typeof(WindowsRescueGuard).GetField("_allowRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(windowsGuard)!);
            var displayContext = typeof(WindowsDisplayService).GetField("_transaction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(windowsDisplay);
            var guardContext = typeof(WindowsRescueGuard).GetField("_transaction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(windowsGuard);
            Assert.NotNull(displayContext);
            Assert.Same(displayContext, guardContext);
        }
        finally { File.Delete(rescue); }
    }

    [Fact]
    public void Missing_rescue_or_invalid_selectors_fail_closed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe");
        Assert.Throws<FileNotFoundException>(() => App.CreateWindowsDisplayServices(Arguments, "journal", false, missing));
        Assert.Throws<ArgumentException>(() => App.CreateWindowsDisplayServices(["--windows-display", "incomplete"], "journal", false, missing));
    }

    [Fact]
    public async Task Disabled_guard_refuses_to_start_rescue_process()
    {
        var guard = new WindowsRescueGuard("unused.exe", "unused.json", TimeSpan.FromSeconds(60));
        await Assert.ThrowsAsync<NotSupportedException>(() => guard.BeginAsync("test"));
    }

    [Fact]
    public void Settings_can_enable_writes_and_selectors_without_cli_args()
    {
        if (!OperatingSystem.IsWindows()) return;
        var rescue = Path.GetTempFileName();
        try
        {
            var journal = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "couchpad-composition-settings.json"));
            var settings = new AppSettings
            {
                AllowDisplayWrites = true,
                TvDevicePath = "tv-path",
                TvEdidManufacturer = 12,
                TvEdidProduct = 34,
                TvExactName = "SAMSUNG",
                InternalDevicePath = "monitor-path",
                InternalEdidManufacturer = 56,
                InternalEdidProduct = 78,
                InternalExactName = "AOC",
            };

            var (display, guard) = App.CreateWindowsDisplayServices(
                [], journal, false, rescue, settings);

            var windowsDisplay = Assert.IsType<WindowsDisplayService>(display);
            var windowsGuard = Assert.IsType<WindowsRescueGuard>(guard);
            var options = (WindowsDisplayOptions)typeof(WindowsDisplayService)
                .GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(windowsDisplay)!;
            Assert.True(options.AllowWrites);
            Assert.Equal("tv-path", options.DevicePath);
            Assert.Equal("SAMSUNG", options.ExactName);
            Assert.Equal("monitor-path", options.InternalDisplay!.DevicePath);
            Assert.True((bool)typeof(WindowsRescueGuard)
                .GetField("_allowRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(windowsGuard)!);
        }
        finally
        {
            File.Delete(rescue);
        }
    }

    [Fact]
    public void Allow_writes_without_selectors_stays_in_simulation()
    {
        if (!OperatingSystem.IsWindows()) return;
        var rescue = Path.GetTempFileName();
        try
        {
            var journal = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "couchpad-composition-missing.json"));
            var (display, guard) = App.CreateWindowsDisplayServices(
                [], journal, false, rescue, new AppSettings { AllowDisplayWrites = true });

            Assert.Null(display);
            Assert.Null(guard);
        }
        finally
        {
            File.Delete(rescue);
        }
    }
}

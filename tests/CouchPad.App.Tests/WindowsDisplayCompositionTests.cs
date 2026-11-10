using System.Reflection;
using CouchPad.App;
using CouchPad.Platform.Windows;

namespace CouchPad.App.Tests;

public class WindowsDisplayCompositionTests
{
    private static readonly string[] Arguments = ["--windows-display", "tv-path", "12", "34", "monitor-path", "56", "78"];

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
}

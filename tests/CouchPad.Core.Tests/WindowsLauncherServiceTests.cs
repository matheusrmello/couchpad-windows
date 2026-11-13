using CouchPad.Core.Launchers;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class WindowsLauncherServiceTests
{
    [Fact]
    public void ReplacementProcessMustNotBeReportedAsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => WindowsLauncherBackend.EnsureNoReplacement(true));
        WindowsLauncherBackend.EnsureNoReplacement(false);
    }

    [Theory]
    [InlineData(LauncherKind.Steam, "-gamepadui")]
    [InlineData(LauncherKind.Hydra, "--big-picture")]
    public void Both_launchers_use_their_big_picture_argument(LauncherKind kind, string expected) =>
        Assert.Equal(expected, WindowsLauncherBackend.ArgumentFor(kind));

    [Theory]
    [InlineData(LauncherKind.Steam)]
    [InlineData(LauncherKind.Hydra)]
    public async Task Existing_launcher_is_never_closed(LauncherKind kind)
    {
        var backend = new FakeBackend { Existing = true };
        var service = new WindowsLauncherService(backend);
        await service.StartAsync(new LauncherSelection(kind));
        await service.CloseOwnedAsync();
        Assert.Equal(1, backend.Starts);
        Assert.False(backend.Tracked);
        Assert.Equal(0, backend.Closes);
        Assert.Equal(kind == LauncherKind.Steam ? "steam://open/bigpicture" : "--big-picture", backend.Argument);
    }

    [Fact]
    public async Task Hydra_closes_only_the_new_window_even_when_the_client_was_already_running()
    {
        var backend = new FakeBackend { Existing = true, NewWindow = new(123, new(42, DateTime.UnixEpoch, "C:\\launcher.exe", LauncherKind.Hydra)) };
        var service = new WindowsLauncherService(backend);
        await service.StartAsync(new LauncherSelection(LauncherKind.Hydra));
        await service.CloseOwnedAsync();
        await service.CloseOwnedAsync();
        Assert.Same(backend.NewWindow, backend.ClosedWindow);
        Assert.Equal(1, backend.WindowCloses);
        Assert.Equal(0, backend.Closes);
    }

    [Fact]
    public async Task Newly_started_launcher_is_closed_once()
    {
        var backend = new FakeBackend();
        var service = new WindowsLauncherService(backend);
        await service.StartAsync(new LauncherSelection(LauncherKind.Steam));
        await service.CloseOwnedAsync();
        await service.CloseOwnedAsync();
        Assert.True(backend.Tracked);
        Assert.Equal(1, backend.Closes);
    }

    [Fact]
    public async Task Failure_to_resolve_does_not_close_any_process()
    {
        var backend = new FakeBackend { Missing = true };
        var service = new WindowsLauncherService(backend);
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.StartAsync(new LauncherSelection(LauncherKind.Hydra)));
        await service.CloseOwnedAsync();
        Assert.Equal(0, backend.Starts);
        Assert.Equal(0, backend.Closes);
    }

    [Fact]
    public async Task Cancellation_during_start_requests_cleanup_of_the_new_owned_process()
    {
        using var started = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var backend = new FakeBackend { StartEntered = started, ResumeStart = resume };
        var service = new WindowsLauncherService(backend);
        var task = service.StartAsync(new LauncherSelection(LauncherKind.Steam), cancellation.Token);
        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        cancellation.Cancel();
        resume.Set();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, backend.Closes);
    }

    private sealed class FakeBackend : IWindowsLauncherBackend
    {
        public bool Existing, Tracked, Missing;
        public int Starts, Closes, WindowCloses;
        public string? Argument;
        public HydraWindow? NewWindow, ClosedWindow;
        public ManualResetEventSlim? StartEntered, ResumeStart;
        public LauncherSpec Resolve(LauncherKind kind) => Missing ? throw new FileNotFoundException() : new("C:\\launcher.exe", WindowsLauncherBackend.ArgumentFor(kind), kind);
        public bool IsRunning(string executable) => Existing;
        public OwnedLauncher? Start(LauncherSpec spec, bool trackOwnership) { Starts++; Argument = spec.Argument; Tracked = trackOwnership; StartEntered?.Set(); ResumeStart?.Wait(); return trackOwnership ? new(42, DateTime.UnixEpoch, spec.Executable, spec.Kind) : null; }
        public bool IsAlive(OwnedLauncher process) => true;
        public IReadOnlyList<HydraWindow> HydraWindows(string executable) => [];
        public Task<HydraWindow?> FindNewHydraWindowAsync(LauncherSpec spec, IReadOnlyList<HydraWindow> before, CancellationToken cancellationToken) => Task.FromResult(NewWindow);
        public Task CloseHydraWindowAsync(HydraWindow window, CancellationToken cancellationToken) { WindowCloses++; ClosedWindow = window; return Task.CompletedTask; }
        public Task CloseAsync(OwnedLauncher process, CancellationToken cancellationToken) { Closes++; return Task.CompletedTask; }
    }
}

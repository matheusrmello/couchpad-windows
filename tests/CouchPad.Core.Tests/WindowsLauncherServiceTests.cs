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

    [Fact]
    public async Task Existing_launcher_is_never_closed()
    {
        var backend = new FakeBackend { Existing = true };
        var service = new WindowsLauncherService(backend);
        await service.StartAsync(new LauncherSelection(LauncherKind.Hydra));
        await service.CloseOwnedAsync();
        Assert.Equal(1, backend.Starts);
        Assert.False(backend.Tracked);
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
        var task = service.StartAsync(new LauncherSelection(LauncherKind.Hydra), cancellation.Token);
        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        cancellation.Cancel();
        resume.Set();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, backend.Closes);
    }

    private sealed class FakeBackend : IWindowsLauncherBackend
    {
        public bool Existing, Tracked, Missing;
        public int Starts, Closes;
        public ManualResetEventSlim? StartEntered, ResumeStart;
        public LauncherSpec Resolve(LauncherKind kind) => Missing ? throw new FileNotFoundException() : new("C:\\launcher.exe", WindowsLauncherBackend.ArgumentFor(kind));
        public bool IsRunning(string executable) => Existing;
        public OwnedLauncher? Start(LauncherSpec spec, bool trackOwnership) { Starts++; Tracked = trackOwnership; StartEntered?.Set(); ResumeStart?.Wait(); return trackOwnership ? new(42, DateTime.UnixEpoch, spec.Executable) : null; }
        public bool IsAlive(OwnedLauncher process) => true;
        public Task CloseAsync(OwnedLauncher process, CancellationToken cancellationToken) { Closes++; return Task.CompletedTask; }
    }
}

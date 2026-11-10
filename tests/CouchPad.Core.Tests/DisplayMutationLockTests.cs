using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class DisplayMutationLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "couchpad-lock-test-" + Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_directory, "display.lock");

    [Fact]
    public async Task Second_writer_waits_until_first_releases()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var first = await DisplayMutationLock.AcquireAsync(PathName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = DisplayMutationLock.AcquireAsync(PathName, timeout.Token).AsTask();
        await Task.Delay(150);
        Assert.False(waiting.IsCompleted);
        await first.DisposeAsync();
        await using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Cancelled_waiter_does_not_release_owner()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var first = await DisplayMutationLock.AcquireAsync(PathName);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await DisplayMutationLock.AcquireAsync(PathName, cancel.Token));
        using var secondCancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await DisplayMutationLock.AcquireAsync(PathName, secondCancel.Token));
    }

    [Fact]
    public async Task Already_cancelled_request_does_not_create_file()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await DisplayMutationLock.AcquireAsync(PathName, cancel.Token));
        Assert.False(File.Exists(PathName));
    }

    [Fact]
    public async Task Release_is_idempotent_and_lock_file_remains()
    {
        if (!OperatingSystem.IsWindows()) return;
        var lease = await DisplayMutationLock.AcquireAsync(PathName);
        lease.Dispose();
        await lease.DisposeAsync();
        Assert.True(File.Exists(PathName));
        await using var next = await DisplayMutationLock.AcquireAsync(PathName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}

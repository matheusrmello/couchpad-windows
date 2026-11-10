using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class DisplayServiceFencingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "couchpad-fence-" + Guid.NewGuid().ToString("N"));
    private const string Hash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private static byte[] PathFor(uint source, uint target)
    {
        var path = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(path, 0); BitConverter.GetBytes(source).CopyTo(path, 8);
        BitConverter.GetBytes(1u).CopyTo(path, 20); BitConverter.GetBytes(target).CopyTo(path, 28);
        BitConverter.GetBytes(1u).CopyTo(path, 60); BitConverter.GetBytes(1u).CopyTo(path, 68);
        return path;
    }
    private static WindowsDisplayService.Snapshot Snapshot(uint flags, params byte[][] paths) => new(
        1, flags, 72, 64, Convert.ToBase64String(paths.SelectMany(p => p).ToArray()), Convert.ToBase64String(new byte[64]),
        [new(1, 0, 10, "tv", "TV", 1, 2, 0), new(1, 0, 20, "monitor", "Monitor", 3, 4, 0)]);

    [Fact]
    public async Task Recovery_request_fences_normal_writer_before_apply()
    {
        if (!OperatingSystem.IsWindows()) return;
        var decisionPath = Path.Combine(_directory, "decision.json");
        var store = new RescueDecisionStore(decisionPath);
        var id = Guid.NewGuid();
        await store.InitializeAsync(id, Hash);
        var context = new RescueTransactionContext(_directory);
        context.Bind(decisionPath, id, Hash);
        await store.TryTransitionAsync(id, Hash, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested);
        var active = Snapshot(2, PathFor(0, 20));
        var all = Snapshot(1, PathFor(0, 20), PathFor(0, 10), PathFor(1, 10));
        var actions = new List<uint>();
        var service = new WindowsDisplayService(
            new("tv", 1, 2, AllowWrites: true, InternalDisplay: new("monitor", 3, 4)),
            flags => flags == 1 ? all : active,
            (paths, modes, action) =>
            {
                actions.Add(action);
                return 0;
            }, context);

        await Assert.ThrowsAsync<RescueDecisionUncertainException>(() => service.ApplyAsync(DisplayMode.TvOnly));
        Assert.Empty(actions);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_directory)) return;
        foreach (var file in Directory.EnumerateFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }
}

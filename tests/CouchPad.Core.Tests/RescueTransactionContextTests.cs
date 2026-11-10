using CouchPad.Core.Automation;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class RescueTransactionContextTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "couchpad-context-" + Guid.NewGuid().ToString("N"));
    private const string Hash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public async Task Fence_requires_matching_armed_decision_and_rejects_recovery()
    {
        var path = Path.Combine(_directory, "decision.json");
        var store = new RescueDecisionStore(path);
        var id = Guid.NewGuid();
        var context = new RescueTransactionContext(_directory);
        await Assert.ThrowsAsync<RescueDecisionUncertainException>(() => context.RequireAsync(RescueDecisionState.Armed));
        await store.InitializeAsync(id, Hash);
        context.Bind(path, id, Hash);
        await context.RequireAsync(RescueDecisionState.Armed);
        Assert.True(await store.TryTransitionAsync(id, Hash, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested));
        await Assert.ThrowsAsync<RescueDecisionUncertainException>(() => context.RequireAsync(RescueDecisionState.Armed));
        await context.RequireAsync(RescueDecisionState.RecoveryRequested);
        Assert.Throws<InvalidOperationException>(() => context.Release(Guid.NewGuid()));
        context.Release(id);
        await Assert.ThrowsAsync<RescueDecisionUncertainException>(() => context.RequireAsync(RescueDecisionState.Armed));
    }
    public void Dispose()
    {
        if (!Directory.Exists(_directory)) return;
        foreach (var file in Directory.EnumerateFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }
}

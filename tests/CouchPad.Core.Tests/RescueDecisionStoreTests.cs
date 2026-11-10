using CouchPad.Platform.Windows;
using CouchPad.Core.Automation;

namespace CouchPad.Core.Tests;

public sealed class RescueDecisionStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "couchpad-decisions-" + Guid.NewGuid().ToString("N"));
    private string DecisionPath => Path.Combine(_directory, "decision.json");
    private const string Digest = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public async Task Initialize_and_commit_are_idempotent_and_cannot_rearm()
    {
        var store = new RescueDecisionStore(DecisionPath);
        var id = Guid.NewGuid();
        Assert.Null(await store.ReadAsync(id, Digest));
        Assert.Equal(RescueDecisionState.Armed, (await store.InitializeAsync(id, Digest)).State);
        Assert.True(await store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.Committed));
        Assert.True(await store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.Committed));
        var reread = await store.InitializeAsync(id, Digest);
        Assert.Equal(RescueDecisionState.Committed, reread.State);
        Assert.Equal(1, reread.Revision);
        Assert.False(await store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested));
    }

    [Fact]
    public async Task Recovery_is_monotonic_and_identity_is_bound_to_baseline()
    {
        var store = new RescueDecisionStore(DecisionPath);
        var id = Guid.NewGuid();
        await store.InitializeAsync(id, Digest);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(Guid.NewGuid(), Digest));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(id, new string('F', 64)));
        Assert.True(await store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested));
        Assert.False(await store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.Aborted));
        Assert.True(await store.TryTransitionAsync(id, Digest, RescueDecisionState.RecoveryRequested, RescueDecisionState.Recovered));
        Assert.Equal(2, (await store.ReadAsync(id, Digest))!.Revision);
        await Assert.ThrowsAsync<ArgumentException>(() => store.TryTransitionAsync(id, Digest, RescueDecisionState.Recovered, RescueDecisionState.Committed));
    }

    [Fact]
    public async Task Competing_commit_and_recovery_have_exactly_one_winner()
    {
        var first = new RescueDecisionStore(DecisionPath);
        var second = new RescueDecisionStore(DecisionPath);
        var id = Guid.NewGuid();
        await first.InitializeAsync(id, Digest);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commit = Task.Run(async () => { await start.Task; return await first.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.Committed); });
        var recover = Task.Run(async () => { await start.Task; return await second.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested); });
        start.SetResult();
        var results = await Task.WhenAll(commit, recover);
        Assert.Single(results, x => x);
        Assert.Equal(results[0] ? RescueDecisionState.Committed : RescueDecisionState.RecoveryRequested, (await first.ReadAsync(id, Digest))!.State);
    }

    [Fact]
    public async Task Corrupt_or_incomplete_record_is_not_overwritten()
    {
        Directory.CreateDirectory(_directory);
        const string invalid = "{\"schemaVersion\":1}";
        await File.WriteAllTextAsync(DecisionPath, invalid);
        var store = new RescueDecisionStore(DecisionPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.InitializeAsync(Guid.NewGuid(), Digest));
        Assert.Equal(invalid, await File.ReadAllTextAsync(DecisionPath));
    }

    [Fact]
    public async Task Lock_wait_honors_cancellation_without_changing_decision()
    {
        var store = new RescueDecisionStore(DecisionPath);
        var id = Guid.NewGuid();
        await store.InitializeAsync(id, Digest);
        await using (var held = new FileStream(DecisionPath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.Committed, cancellation.Token));
        }
        Assert.Equal(RescueDecisionState.Armed, (await store.ReadAsync(id, Digest))!.State);
    }

    [Fact]
    public async Task Guarded_action_blocks_recovery_transition_until_action_finishes()
    {
        var store = new RescueDecisionStore(DecisionPath);
        var competing = new RescueDecisionStore(DecisionPath);
        var id = Guid.NewGuid();
        await store.InitializeAsync(id, Digest);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var running = Task.Run(() => store.ExecuteIfStateAsync(id, Digest, RescueDecisionState.Armed, () =>
        {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Action was not released.");
        }));

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => competing.TryTransitionAsync(
                id, Digest, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested, timeout.Token));
            Assert.False(running.IsCompleted);
        }
        finally
        {
            release.Set();
            await running;
        }

        Assert.True(await competing.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested));
    }

    [Fact]
    public async Task Guarded_action_does_not_run_after_recovery_is_requested()
    {
        var store = new RescueDecisionStore(DecisionPath);
        var id = Guid.NewGuid();
        await store.InitializeAsync(id, Digest);
        Assert.True(await store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested));
        var called = false;
        await Assert.ThrowsAsync<RescueDecisionUncertainException>(() =>
            store.ExecuteIfStateAsync(id, Digest, RescueDecisionState.Armed, () => called = true));
        Assert.False(called);
    }

    [Fact]
    public async Task Guarded_action_exception_releases_decision_lock()
    {
        var store = new RescueDecisionStore(DecisionPath);
        var id = Guid.NewGuid();
        await store.InitializeAsync(id, Digest);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ExecuteIfStateAsync(id, Digest, RescueDecisionState.Armed, () => throw new InvalidOperationException()));
        Assert.True(await store.TryTransitionAsync(id, Digest, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_directory)) return;
        foreach (var file in Directory.EnumerateFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }
}

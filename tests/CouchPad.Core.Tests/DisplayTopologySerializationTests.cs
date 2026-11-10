using System.Text.Json;
using CouchPad.Platform.Windows;
using CouchPad.Core.Display;

namespace CouchPad.Core.Tests;

public class DisplayTopologySerializationTests
{
    [Fact]
    public void Snapshot_roundtrips_binary_data_and_target_identity()
    {
        var expected = new WindowsDisplayService.Snapshot(1, 2, 72, 64, Convert.ToBase64String(new byte[] { 0, 1, 255 }), Convert.ToBase64String(new byte[] { 5, 0 }), [new(1, -1, 3, "path", "name", 42, 12, 0)]);
        var actual = JsonSerializer.Deserialize<WindowsDisplayService.Snapshot>(JsonSerializer.Serialize(expected));
        Assert.NotNull(actual);
        Assert.Equal(expected.PathsBase64, actual.PathsBase64);
        Assert.Equal(expected.ModesBase64, actual.ModesBase64);
        Assert.Equal(expected.Targets, actual.Targets);
    }
}

public class DisplaySelectionTests
{
    private static WindowsDisplayService.Target Target(uint id, string? path, string? name, ushort? manufacturer, ushort? product) => new(1, 0, id, path, name, manufacturer, product, 0);

    [Fact]
    public void Selects_identity_and_deduplicates_paths()
    {
        var target = Target(4, "path", "TV", 12, 34);
        Assert.Equal(target, WindowsDisplayService.SelectTarget([target, target], "PATH", 12, 34));
    }

    [Fact]
    public void Uses_exact_name_only_when_identity_is_missing()
    {
        var target = Target(4, null, "TV", null, null);
        Assert.Equal(target, WindowsDisplayService.SelectTarget([target], "path", 12, 34, "tv"));
        Assert.Throws<InvalidOperationException>(() => WindowsDisplayService.SelectTarget([Target(4, "other", "TV", 12, 34)], "path", 12, 34, "TV"));
    }

    [Fact]
    public void Refuses_ambiguous_fallback()
    {
        Assert.Throws<InvalidOperationException>(() => WindowsDisplayService.SelectTarget([Target(1, null, "TV", null, null), Target(2, null, "TV", null, null)], "path", 12, 34, "TV"));
    }
}

public class DisplayMappingTests
{
    private static WindowsDisplayService.Target Tv => new(1, 0, 10, "tv", "TV", 1, 2, 0);
    private static byte[] Path(uint source, uint target)
    {
        var data = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(data, 0);
        BitConverter.GetBytes(source).CopyTo(data, 8);
        BitConverter.GetBytes(1u).CopyTo(data, 20);
        BitConverter.GetBytes(target).CopyTo(data, 28);
        return data;
    }
    private static WindowsDisplayService.Snapshot Snapshot(params byte[][] paths) => new(1, 2, 72, 64, Convert.ToBase64String(paths.SelectMany(x => x).ToArray()), "", []);

    [Theory]
    [InlineData(0, DisplayMode.InternalOnly, true, false)]
    [InlineData(1, DisplayMode.TvOnly, false, true)]
    [InlineData(2, DisplayMode.Extend, true, true)]
    [InlineData(3, DisplayMode.Mirror, true, true)]
    public void Maps_active_paths(int scenario, CouchPad.Core.Display.DisplayMode expected, bool primary, bool tv)
    {
        var paths = scenario switch { 0 => new[] { Path(0, 20) }, 1 => new[] { Path(0, 10) }, 2 => new[] { Path(0, 20), Path(1, 10) }, _ => new[] { Path(0, 20), Path(0, 10) } };
        var result = WindowsDisplayService.MapState(Snapshot(paths), Tv);
        Assert.Equal(expected, result.Mode);
        Assert.Equal(primary, result.PrimaryActive);
        Assert.Equal(tv, result.TvActive);
        Assert.NotNull(result.Topology);
    }

    [Fact]
    public void Refuses_more_than_one_non_tv_target() => Assert.Throws<InvalidOperationException>(() => WindowsDisplayService.MapState(Snapshot(Path(0, 20), Path(1, 30)), Tv));

    [Fact]
    public void Writes_are_disabled_by_default()
    {
        var service = new WindowsDisplayService(new("tv", 1, 2));
        Assert.Throws<NotSupportedException>(() => { _ = service.ApplyAsync(CouchPad.Core.Display.DisplayMode.TvOnly); });
    }
}



public class DisplayTopologyPlannerTests
{
    private static byte[] Path(uint source, uint target, bool available = true)
    {
        var bytes = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(bytes, 0);
        BitConverter.GetBytes(source).CopyTo(bytes, 8);
        BitConverter.GetBytes(1u).CopyTo(bytes, 20);
        BitConverter.GetBytes(target).CopyTo(bytes, 28);
        BitConverter.GetBytes(available ? 1u : 0u).CopyTo(bytes, 60);
        return bytes;
    }
    private static WindowsDisplayService.Snapshot Snapshot(uint flags, params byte[][] paths) => new(1, flags, 72, 64, Convert.ToBase64String(paths.SelectMany(x => x).ToArray()), "", []);
    private static WindowsDisplayService.Target Tv => new(1, 0, 10, "tv", "TV", 1, 2, 0);

    private static WindowsDisplayService.Target Internal => new(1, 0, 20, "internal", "Monitor", 3, 4, 0);

    [Theory]
    [InlineData(DisplayMode.TvOnly, 1, 0)]
    [InlineData(DisplayMode.Mirror, 2, 0)]
    [InlineData(DisplayMode.Extend, 2, 1)]
    public void Builds_proposal_for_inactive_tv(DisplayMode mode, int count, uint tvSource)
    {
        var baseline = Snapshot(2, Path(0, 20));
        var all = Snapshot(1, Path(0, 20), Path(0, 10), Path(1, 10));
        var result = DisplayTopologyPlanner.Create(baseline, all, Tv, Internal, mode);
        Assert.Equal(72 * count, result.Paths.Length);
        var last = (count - 1) * 72;
        Assert.Equal(tvSource, BitConverter.ToUInt32(result.Paths, last + 8));
        Assert.Equal(10u, BitConverter.ToUInt32(result.Paths, last + 28));
        Assert.Equal(uint.MaxValue, BitConverter.ToUInt32(result.Paths, last + 12));
        Assert.Equal(uint.MaxValue, BitConverter.ToUInt32(result.Paths, last + 32));
        Assert.Equal(1u, BitConverter.ToUInt32(result.Paths, last + 68) & 1);
        Assert.Equal(0u, BitConverter.ToUInt32(Convert.FromBase64String(all.PathsBase64), 72 + 68));
    }

    [Fact]
    public void Rejects_unavailable_target() => Assert.Throws<InvalidOperationException>(() => DisplayTopologyPlanner.Create(Snapshot(2, Path(0, 20)), Snapshot(1, Path(0, 20), Path(0, 10, false)), Tv, Internal, DisplayMode.Mirror));

    [Fact]
    public void Rejects_missing_distinct_source_for_extend() => Assert.Throws<InvalidOperationException>(() => DisplayTopologyPlanner.Create(Snapshot(2, Path(0, 20)), Snapshot(1, Path(0, 20), Path(0, 10)), Tv, Internal, DisplayMode.Extend));
}

public class DisplayTransactionTests
{
    [Fact]
    public void Failure_after_apply_rolls_back()
    {
        var steps = new List<string>();
        Assert.Throws<InvalidOperationException>(() => DisplayTransaction.Execute(() => steps.Add("validate"), () => steps.Add("apply"), () => throw new InvalidOperationException(), () => steps.Add("rollback"), default));
        Assert.Equal(new[] { "validate", "apply", "rollback" }, steps);
    }
    [Fact]
    public void Validation_failure_does_not_mutate_or_rollback()
    {
        var steps = new List<string>();
        Assert.Throws<InvalidOperationException>(() => DisplayTransaction.Execute(() => throw new InvalidOperationException(), () => steps.Add("apply"), () => { }, () => steps.Add("rollback"), default));
        Assert.Empty(steps);
    }
    [Fact]
    public void Cancellation_after_apply_rolls_back()
    {
        using var source = new CancellationTokenSource();
        var rolledBack = false;
        Assert.Throws<OperationCanceledException>(() => DisplayTransaction.Execute(() => { }, source.Cancel, () => { }, () => rolledBack = true, source.Token));
        Assert.True(rolledBack);
    }
    [Fact]
    public void Rollback_failure_preserves_both_errors()
    {
        var error = Assert.Throws<AggregateException>(() => DisplayTransaction.Execute(() => { }, () => throw new InvalidOperationException("apply"), () => { }, () => throw new InvalidOperationException("rollback"), default));
        Assert.Equal(2, error.InnerExceptions.Count);
    }
}

public class WindowsDisplayServiceFakeTests
{
    private static byte[] Path(uint source, uint target)
    {
        var bytes = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(bytes, 0);
        BitConverter.GetBytes(source).CopyTo(bytes, 8);
        BitConverter.GetBytes(1u).CopyTo(bytes, 20);
        BitConverter.GetBytes(target).CopyTo(bytes, 28);
        BitConverter.GetBytes(1u).CopyTo(bytes, 32);
        BitConverter.GetBytes(1u).CopyTo(bytes, 60);
        BitConverter.GetBytes(1u).CopyTo(bytes, 68);
        return bytes;
    }
    private static WindowsDisplayService.Snapshot Snapshot(uint flags, params byte[][] paths)
    {
        var modes = new byte[128];
        BitConverter.GetBytes(1u).CopyTo(modes, 0);
        BitConverter.GetBytes(1u).CopyTo(modes, 8);
        BitConverter.GetBytes(2u).CopyTo(modes, 64);
        BitConverter.GetBytes(20u).CopyTo(modes, 68);
        BitConverter.GetBytes(1u).CopyTo(modes, 72);
        return new(1, flags, 72, 64, Convert.ToBase64String(paths.SelectMany(p => p).ToArray()), Convert.ToBase64String(modes), [new(1, 0, 10, "tv", "TV", 1, 2, 0), new(1, 0, 20, "internal", "Monitor", 3, 4, 0)]);
    }
    private static WindowsDisplayService Create(Func<byte[], byte[], uint, int> set)
    {
        var active = Snapshot(2, Path(0, 20));
        var all = Snapshot(1, Path(0, 20), Path(0, 10), Path(1, 10));
        return new(new("tv", 1, 2, AllowWrites: true, InternalDisplay: new("internal", 3, 4)), flags => flags == 1 ? all : active, set);
    }
    [Fact]
    public void Validation_failure_prevents_apply()
    {
        var actions = new List<uint>();
        var service = Create((p, m, action) => { actions.Add(action); return 87; });
        Assert.Throws<System.ComponentModel.Win32Exception>(() => { _ = service.ApplyAsync(DisplayMode.TvOnly); });
        Assert.Equal(new uint[] { 0x40 }, actions);
    }
    [Fact]
    public void Verification_failure_restores_baseline()
    {
        var actions = new List<uint>();
        var service = Create((p, m, action) => { actions.Add(action); return 0; });
        Assert.Throws<InvalidOperationException>(() => { _ = service.ApplyAsync(DisplayMode.TvOnly); });
        Assert.Equal(new uint[] { 0x40, 0x80, 0x40, 0x80 }, actions);
    }
    [Fact]
    public void Cancel_after_apply_restores_baseline()
    {
        using var source = new CancellationTokenSource();
        var actions = new List<uint>();
        var service = Create((p, m, action) => { actions.Add(action); if (actions.Count == 2) source.Cancel(); return 0; });
        Assert.Throws<OperationCanceledException>(() => { _ = service.ApplyAsync(DisplayMode.TvOnly, source.Token); });
        Assert.Equal(new uint[] { 0x40, 0x80, 0x40, 0x80 }, actions);
    }
}

public class DisplayTopologyFromEveryBaselineTests
{
    private static byte[] Path(uint source, uint target)
    {
        var b = new byte[72];
        BitConverter.GetBytes(1u).CopyTo(b, 0);
        BitConverter.GetBytes(source).CopyTo(b, 8);
        BitConverter.GetBytes(1u).CopyTo(b, 20);
        BitConverter.GetBytes(target).CopyTo(b, 28);
        BitConverter.GetBytes(1u).CopyTo(b, 60);
        return b;
    }
    private static WindowsDisplayService.Snapshot Snapshot(uint flags, params byte[][] paths) => new(1, flags, 72, 64, Convert.ToBase64String(paths.SelectMany(x => x).ToArray()), "", []);
    [Theory]
    [InlineData(0, DisplayMode.TvOnly)] [InlineData(0, DisplayMode.Extend)] [InlineData(0, DisplayMode.Mirror)]
    [InlineData(1, DisplayMode.TvOnly)] [InlineData(1, DisplayMode.Extend)] [InlineData(1, DisplayMode.Mirror)]
    [InlineData(2, DisplayMode.TvOnly)] [InlineData(2, DisplayMode.Extend)] [InlineData(2, DisplayMode.Mirror)]
    [InlineData(3, DisplayMode.TvOnly)] [InlineData(3, DisplayMode.Extend)] [InlineData(3, DisplayMode.Mirror)]
    public void Plans_from_any_baseline(int baselineMode, DisplayMode requested)
    {
        var current = baselineMode switch { 0 => new[] { Path(0, 20) }, 1 => new[] { Path(0, 10) }, 2 => new[] { Path(0, 20), Path(1, 10) }, _ => new[] { Path(0, 20), Path(0, 10) } };
        var all = Snapshot(1, Path(0, 20), Path(1, 20), Path(0, 10), Path(1, 10));
        var tv = new WindowsDisplayService.Target(1, 0, 10, "tv", "TV", 1, 2, 0);
        var monitor = new WindowsDisplayService.Target(1, 0, 20, "monitor", "Monitor", 3, 4, 0);
        var result = DisplayTopologyPlanner.Create(Snapshot(2, current), all, tv, monitor, requested);
        Assert.Equal(requested == DisplayMode.TvOnly ? 72 : 144, result.Paths.Length);
        if (requested != DisplayMode.TvOnly)
            Assert.Equal(requested == DisplayMode.Mirror, BitConverter.ToUInt32(result.Paths, 8) == BitConverter.ToUInt32(result.Paths, 80));
    }
}

public class DisplaySnapshotComparerTests
{
    private static WindowsDisplayService.Snapshot Create()
    {
        var path = new byte[72]; var modes = new byte[128];
        BitConverter.GetBytes(1u).CopyTo(path, 0); BitConverter.GetBytes(7u).CopyTo(path, 8);
        BitConverter.GetBytes(1u).CopyTo(path, 20); BitConverter.GetBytes(10u).CopyTo(path, 28);
        BitConverter.GetBytes(1u).CopyTo(path, 32); BitConverter.GetBytes(1u).CopyTo(path, 68);
        BitConverter.GetBytes(1u).CopyTo(modes, 0); BitConverter.GetBytes(7u).CopyTo(modes, 4); BitConverter.GetBytes(1u).CopyTo(modes, 8);
        BitConverter.GetBytes(1920u).CopyTo(modes, 16);
        BitConverter.GetBytes(2u).CopyTo(modes, 64); BitConverter.GetBytes(10u).CopyTo(modes, 68); BitConverter.GetBytes(1u).CopyTo(modes, 72);
        return new(1, 2, 72, 64, Convert.ToBase64String(path), Convert.ToBase64String(modes), []);
    }
    [Fact]
    public void Ignores_status_but_detects_mode_and_identity_changes()
    {
        var expected = Create(); var paths = Convert.FromBase64String(expected.PathsBase64);
        BitConverter.GetBytes(99u).CopyTo(paths, 16);
        Assert.True(DisplaySnapshotComparer.SemanticallyEqual(expected, expected with { PathsBase64 = Convert.ToBase64String(paths) }));
        var modes = Convert.FromBase64String(expected.ModesBase64); BitConverter.GetBytes(1280u).CopyTo(modes, 16);
        Assert.False(DisplaySnapshotComparer.SemanticallyEqual(expected, expected with { ModesBase64 = Convert.ToBase64String(modes) }));
        BitConverter.GetBytes(11u).CopyTo(paths, 28);
        Assert.Throws<InvalidOperationException>(() => DisplaySnapshotComparer.SemanticallyEqual(expected, expected with { PathsBase64 = Convert.ToBase64String(paths) }));
    }
}

public class RescueWatchdogLoopTests
{
    [Theory]
    [InlineData(RescueWatchdogLoop.Signal.Commit)]
    [InlineData(RescueWatchdogLoop.Signal.Abort)]
    public async Task Release_signal_does_not_restore(RescueWatchdogLoop.Signal signal)
    {
        var pending = new TaskCompletionSource(); var restored = false;
        var outcome = await RescueWatchdogLoop.RunAsync(pending.Task, pending.Task, Task.FromResult(signal), () => { restored = true; return Task.CompletedTask; });
        Assert.Equal(RescueWatchdogLoop.Outcome.Released, outcome);
        Assert.False(restored);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task Death_timeout_or_abandon_restores(int condition)
    {
        var pending = new TaskCompletionSource(); var signal = new TaskCompletionSource<RescueWatchdogLoop.Signal>(); var restored = 0;
        var parent = condition == 0 ? Task.CompletedTask : pending.Task;
        var timeout = condition == 1 ? Task.CompletedTask : pending.Task;
        if (condition == 2) signal.SetResult(RescueWatchdogLoop.Signal.Abandon);
        var outcome = await RescueWatchdogLoop.RunAsync(parent, timeout, signal.Task, () => { restored++; return Task.CompletedTask; });
        Assert.Equal(RescueWatchdogLoop.Outcome.Restored, outcome);
        Assert.Equal(1, restored);
    }
    [Fact]
    public async Task Simultaneous_death_and_commit_fails_closed()
    {
        var restored = false;
        var outcome = await RescueWatchdogLoop.RunAsync(Task.CompletedTask, new TaskCompletionSource().Task, Task.FromResult(RescueWatchdogLoop.Signal.Commit), () => { restored = true; return Task.CompletedTask; });
        Assert.Equal(RescueWatchdogLoop.Outcome.Restored, outcome);
        Assert.True(restored);
    }
}

public class RescueFailureLogTests
{
    [Fact]
    public void Writes_local_stage_without_exception_message_or_unknown_stage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "couchpad-rescue-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            RescueFailureLog.TryWrite(directory, "restore", new InvalidOperationException("private payload"));
            RescueFailureLog.TryWrite(directory, "untrusted text", new IOException("private payload"));
            var log = File.ReadAllText(Path.Combine(directory, "rescue-failures.log"));
            Assert.Contains("stage=restore", log);
            Assert.Contains("stage=unknown", log);
            Assert.DoesNotContain("private payload", log);
            Assert.DoesNotContain("untrusted text", log);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}

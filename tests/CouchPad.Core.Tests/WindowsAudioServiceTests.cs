using System.Runtime.Versioning;
using CouchPad.Core.Audio;
using CouchPad.Core.Automation;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "couchpad-audio-fence-" + Guid.NewGuid().ToString("N"));
    private const string Hash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private static readonly AudioBaseline Baseline = new(1, "monitor", "monitor", "monitor", [new("monitor", .5f, false)]);

    [Fact]
    public async Task Armed_transaction_sets_and_verifies_all_roles()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (context, _, _) = await Armed();
        var backend = new Backend();
        var service = Create(context, backend, true, () => Inventory("tv"));
        Assert.Equal(AudioOutput.Hdmi, await service.ApplyAsync(AudioMode.MoveToHdmi));
        Assert.All(Enum.GetValues<AudioRole>(), role => Assert.Equal("tv", backend.GetDefault(role)));
    }

    [Fact]
    public async Task Recovery_decision_prevents_audio_writes()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (context, store, id) = await Armed();
        await store.TryTransitionAsync(id, Hash, RescueDecisionState.Armed, RescueDecisionState.RecoveryRequested);
        var backend = new Backend();
        var service = Create(context, backend, true, () => Inventory("tv"));
        await Assert.ThrowsAsync<RescueDecisionUncertainException>(() => service.ApplyAsync(AudioMode.MoveToHdmi));
        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public async Task Disabled_gate_missing_and_ambiguous_targets_do_not_write()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (context, _, _) = await Armed();
        var backend = new Backend();
        var disabled = Create(context, backend, false, () => Inventory("tv"));
        await Assert.ThrowsAsync<NotSupportedException>(() => disabled.ApplyAsync(AudioMode.MoveToHdmi));
        var missing = Create(context, backend, true, () => Inventory());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => missing.ApplyAsync(AudioMode.MoveToHdmi));
        var ambiguous = Create(context, backend, true, () => Inventory("tv", "tv"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ambiguous.ApplyAsync(AudioMode.MoveToHdmi));
        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public async Task Verification_failure_is_reported_for_coordinator_rollback()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (context, _, _) = await Armed();
        var backend = new Backend { WrongDefault = true };
        var service = Create(context, backend, true, () => Inventory("tv"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(AudioMode.MoveToHdmi));
    }

    [Fact]
    public async Task Restores_complete_baseline_under_armed_transaction()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (context, _, _) = await Armed();
        var backend = new Backend();
        var service = Create(context, backend, true, () => Inventory("tv"));
        await service.RestoreBaselineAsync(Baseline);
        Assert.All(Enum.GetValues<AudioRole>(), role => Assert.Equal("monitor", backend.GetDefault(role)));
        Assert.Equal((.5f, false), backend.GetVolumeAndMute("monitor"));
    }

    [Fact]
    public async Task Revocation_blocks_new_audio_apply_but_keeps_restore_available()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (context, _, _) = await Armed();
        var backend = new Backend();
        var service = Create(context, backend, true, () => Inventory("tv"));
        service.DisableNewWrites();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyAsync(AudioMode.MoveToHdmi));
        Assert.Equal(0, backend.Writes);
        await service.RestoreBaselineAsync(Baseline);
        Assert.Equal("monitor", backend.GetDefault(AudioRole.Console));
    }

    [SupportedOSPlatform("windows")]
    private static WindowsAudioService Create(RescueTransactionContext context, Backend backend, bool allow, Func<WindowsAudioProbe.Inventory> inventory) =>
        new(new WindowsAudioOptions("tv", allow, TimeSpan.FromMilliseconds(40)), context, backend, inventory, () => Baseline);

    private static WindowsAudioProbe.Inventory Inventory(params string[] ids) =>
        new(ids.Select(id => new WindowsAudioProbe.Endpoint(id, 1, id)).ToArray(), "monitor", "monitor", "monitor");

    private async Task<(RescueTransactionContext Context, RescueDecisionStore Store, Guid Id)> Armed()
    {
        var path = Path.Combine(_directory, "decision.json");
        var store = new RescueDecisionStore(path);
        var id = Guid.NewGuid();
        await store.InitializeAsync(id, Hash);
        var context = new RescueTransactionContext(_directory);
        context.Bind(path, id, Hash);
        return (context, store, id);
    }

    private sealed class Backend : IAudioRecoveryBackend
    {
        private readonly Dictionary<AudioRole, string> _roles = [];
        private readonly Dictionary<string, (float Volume, bool Muted)> _volumes = [];
        public int Writes { get; private set; }
        public bool WrongDefault { get; init; }
        public bool IsActive(string endpointId) => true;
        public void SetDefault(string endpointId, AudioRole role) { Writes++; _roles[role] = endpointId; }
        public string? GetDefault(AudioRole role) => WrongDefault ? "wrong" : _roles.GetValueOrDefault(role);
        public void SetVolumeAndMute(string endpointId, float volume, bool muted) { Writes++; _volumes[endpointId] = (volume, muted); }
        public (float Volume, bool Muted) GetVolumeAndMute(string endpointId) => _volumes[endpointId];
    }

    public void Dispose()
    {
        if (!Directory.Exists(_directory)) return;
        foreach (var file in Directory.EnumerateFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }
}

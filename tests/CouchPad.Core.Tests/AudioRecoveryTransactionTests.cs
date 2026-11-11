using CouchPad.Core.Audio;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class AudioRecoveryTransactionTests
{
    private static AudioBaseline Baseline => new(1, "a", "b", "a", [new("a", 0.5f, false), new("b", 0.2f, true)]);

    [Fact]
    public void Restores_and_verifies_each_role_and_unique_endpoint()
    {
        var backend = new FakeBackend();
        AudioRecoveryTransaction.Restore(Baseline, backend);
        Assert.Equal("a", backend.Roles[AudioRole.Console]);
        Assert.Equal("b", backend.Roles[AudioRole.Multimedia]);
        Assert.Equal("a", backend.Roles[AudioRole.Communications]);
        Assert.Equal((0.2f, true), backend.Volumes["b"]);
        Assert.Equal(5, backend.Writes);
    }

    [Fact]
    public void Missing_endpoint_or_invalid_snapshot_does_not_write()
    {
        var backend = new FakeBackend { Missing = "b" };
        Assert.Throws<InvalidOperationException>(() => AudioRecoveryTransaction.Restore(Baseline, backend));
        Assert.Equal(0, backend.Writes);
        Assert.Throws<InvalidDataException>(() => AudioRecoveryTransaction.Restore(Baseline with { SchemaVersion = 9 }, backend));
        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public void Verification_failure_propagates_without_signalling_success()
    {
        var backend = new FakeBackend { WrongDefault = true };
        Assert.Throws<InvalidOperationException>(() => AudioRecoveryTransaction.Restore(Baseline, backend));
        Assert.Equal(1, backend.Writes);
    }

    [Fact]
    public void Detects_a_later_role_change_that_invalidates_an_earlier_default()
    {
        var backend = new FakeBackend { CorruptEarlierRole = true };
        Assert.Throws<InvalidOperationException>(() => AudioRecoveryTransaction.Restore(Baseline, backend));
    }

    [Fact]
    public async Task Waits_for_baseline_endpoints_then_restores()
    {
        var backend = new FakeBackend { MissingChecks = 2 };
        await AudioRecoveryTransaction.RestoreWhenAvailableAsync(Baseline, backend, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(5));
        Assert.Equal(5, backend.Writes);
    }

    [Fact]
    public async Task Times_out_without_writes_when_endpoint_stays_unavailable()
    {
        var backend = new FakeBackend { Missing = "b" };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AudioRecoveryTransaction.RestoreWhenAvailableAsync(
            Baseline, backend, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(5)));
        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public void Windows_backend_rejects_mutations_by_default_before_touching_devices()
    {
        if (!OperatingSystem.IsWindows()) return;
        AssertWindowsBackendIsReadOnly();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void AssertWindowsBackendIsReadOnly()
    {
        var backend = new WindowsAudioRecoveryBackend();
        Assert.Throws<NotSupportedException>(() => backend.SetDefault("unused", AudioRole.Console));
        Assert.Throws<NotSupportedException>(() => backend.SetVolumeAndMute("unused", 0.5f, false));
    }

    private sealed class FakeBackend : IAudioRecoveryBackend
    {
        public string? Missing { get; init; }
        public bool WrongDefault { get; init; }
        public bool CorruptEarlierRole { get; init; }
        public int MissingChecks { get; set; }
        public int Writes { get; private set; }
        public Dictionary<AudioRole, string> Roles { get; } = [];
        public Dictionary<string, (float Volume, bool Muted)> Volumes { get; } = [];
        public bool IsActive(string endpointId)
        {
            if (MissingChecks > 0) { MissingChecks--; return false; }
            return endpointId != Missing;
        }
        public void SetDefault(string endpointId, AudioRole role)
        {
            Writes++;
            Roles[role] = endpointId;
            if (CorruptEarlierRole && role == AudioRole.Communications) Roles[AudioRole.Console] = "wrong";
        }
        public string? GetDefault(AudioRole role) => WrongDefault ? "wrong" : Roles.GetValueOrDefault(role);
        public void SetVolumeAndMute(string endpointId, float volume, bool muted) { Writes++; Volumes[endpointId] = (volume, muted); }
        public (float Volume, bool Muted) GetVolumeAndMute(string endpointId) => Volumes[endpointId];
    }
}

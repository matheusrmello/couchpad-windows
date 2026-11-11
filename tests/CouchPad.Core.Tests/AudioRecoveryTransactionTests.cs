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

    private sealed class FakeBackend : IAudioRecoveryBackend
    {
        public string? Missing { get; init; }
        public bool WrongDefault { get; init; }
        public int Writes { get; private set; }
        public Dictionary<AudioRole, string> Roles { get; } = [];
        public Dictionary<string, (float Volume, bool Muted)> Volumes { get; } = [];
        public bool IsActive(string endpointId) => endpointId != Missing;
        public void SetDefault(string endpointId, AudioRole role) { Writes++; Roles[role] = endpointId; }
        public string? GetDefault(AudioRole role) => WrongDefault ? "wrong" : Roles.GetValueOrDefault(role);
        public void SetVolumeAndMute(string endpointId, float volume, bool muted) { Writes++; Volumes[endpointId] = (volume, muted); }
        public (float Volume, bool Muted) GetVolumeAndMute(string endpointId) => Volumes[endpointId];
    }
}

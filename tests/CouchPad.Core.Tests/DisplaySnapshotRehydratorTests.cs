using System.Text.Json;
using CouchPad.Core.Display;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public class DisplaySnapshotRehydratorTests
{
    private const uint OldAdapter = 59140;
    private const uint NewAdapter = 59029;
    private const uint SourceId = 0;
    private const uint TargetId = 24646;
    private const string DevicePath = @"\\?\DISPLAY#AOC2702#7&350abd5e&1&UID24646#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    private static WindowsDisplayService.Snapshot Snapshot(
        uint adapterLow,
        uint queryFlags,
        bool available = true,
        uint targetId = TargetId,
        string devicePath = DevicePath)
    {
        var path = new byte[72];
        WriteKey(path, 0, adapterLow, SourceId);
        WriteKey(path, 20, adapterLow, targetId);
        BitConverter.GetBytes(1u).CopyTo(path, 12);
        BitConverter.GetBytes(0u).CopyTo(path, 32);
        BitConverter.GetBytes(available ? 1u : 0u).CopyTo(path, 60);
        BitConverter.GetBytes(1u).CopyTo(path, 68);

        var modes = new byte[128];
        // Target mode 0.
        BitConverter.GetBytes(2u).CopyTo(modes, 0);
        BitConverter.GetBytes(targetId).CopyTo(modes, 4);
        BitConverter.GetBytes(adapterLow).CopyTo(modes, 8);
        // Source mode 1.
        BitConverter.GetBytes(1u).CopyTo(modes, 64);
        BitConverter.GetBytes(SourceId).CopyTo(modes, 68);
        BitConverter.GetBytes(adapterLow).CopyTo(modes, 72);
        BitConverter.GetBytes(1920u).CopyTo(modes, 80);
        BitConverter.GetBytes(1080u).CopyTo(modes, 84);

        return new(
            1,
            queryFlags,
            72,
            64,
            Convert.ToBase64String(path),
            Convert.ToBase64String(modes),
            [new(adapterLow, 0, targetId, devicePath, "27G2G4", 58117, 9986, 0)]);
    }

    [Fact]
    public void Rebinds_stale_adapter_and_source_identities()
    {
        var journal = Snapshot(OldAdapter, 2);
        var current = Snapshot(NewAdapter, 2);
        var available = Snapshot(NewAdapter, 1);

        var rehydrated = DisplaySnapshotRehydrator.Rehydrate(journal, available);

        Assert.True(DisplaySnapshotComparer.SemanticallyEqual(rehydrated, current));
        var paths = Convert.FromBase64String(rehydrated.PathsBase64);
        Assert.Equal(NewAdapter, BitConverter.ToUInt32(paths, 0));
        Assert.Equal(NewAdapter, BitConverter.ToUInt32(paths, 20));
        var modes = Convert.FromBase64String(rehydrated.ModesBase64);
        Assert.Equal(NewAdapter, BitConverter.ToUInt32(modes, 8));
        Assert.Equal(NewAdapter, BitConverter.ToUInt32(modes, 72));
    }

    [Fact]
    public async Task Restore_skips_write_when_rehydrated_target_is_already_current()
    {
        var journal = Snapshot(OldAdapter, 2);
        var current = Snapshot(NewAdapter, 2);
        var available = Snapshot(NewAdapter, 1);
        var actions = new List<uint>();
        var service = new WindowsDisplayService(
            new("", 0, 0, AllowWrites: true),
            flags => flags == 1 ? available : current,
            (_, _, action) => { actions.Add(action); return 0; });

        await service.RestoreAsync(new DisplayState(DisplayMode.InternalOnly, true, false)
        {
            Topology = JsonSerializer.Serialize(journal),
        });

        Assert.Empty(actions);
    }

    [Fact]
    public async Task Restore_applies_rehydrated_snapshot_when_current_differs()
    {
        var journal = Snapshot(OldAdapter, 2);
        var available = Snapshot(NewAdapter, 1);
        var other = Snapshot(NewAdapter, 2, targetId: 41015, devicePath: "other");
        var target = Snapshot(NewAdapter, 2);
        var active = other;
        var actions = new List<uint>();
        var service = new WindowsDisplayService(
            new("", 0, 0, AllowWrites: true),
            flags => flags == 1 ? available : active,
            (paths, _, action) =>
            {
                actions.Add(action);
                if (action == 0x80)
                {
                    Assert.True(DisplaySnapshotComparer.SemanticallyEqual(target,
                        target with { PathsBase64 = Convert.ToBase64String(paths) }));
                    active = target;
                }
                return 0;
            });

        await service.RestoreAsync(new DisplayState(DisplayMode.InternalOnly, true, false)
        {
            Topology = JsonSerializer.Serialize(journal),
        });

        Assert.Equal(new uint[] { 0x40, 0x80 }, actions);
    }

    [Fact]
    public void Keeps_same_identity_when_target_metadata_is_missing()
    {
        var journal = Snapshot(NewAdapter, 2) with { Targets = [] };
        var current = Snapshot(NewAdapter, 2);
        var available = Snapshot(NewAdapter, 1);

        var rehydrated = DisplaySnapshotRehydrator.Rehydrate(journal, available);

        Assert.True(DisplaySnapshotComparer.SemanticallyEqual(rehydrated, current));
    }

    [Fact]
    public void Rejects_target_that_is_no_longer_available()
    {
        var journal = Snapshot(OldAdapter, 2);
        var available = Snapshot(NewAdapter, 1, available: false);

        Assert.Throws<InvalidOperationException>(() => DisplaySnapshotRehydrator.Rehydrate(journal, available));
    }

    private static void WriteKey(byte[] path, int offset, uint adapterLow, uint id)
    {
        BitConverter.GetBytes(adapterLow).CopyTo(path, offset);
        BitConverter.GetBytes(0).CopyTo(path, offset + 4);
        BitConverter.GetBytes(id).CopyTo(path, offset + 8);
    }
}

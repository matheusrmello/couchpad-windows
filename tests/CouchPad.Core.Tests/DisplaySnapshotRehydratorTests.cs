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
        string devicePath = DevicePath,
        uint sourceId = SourceId)
    {
        var path = new byte[72];
        WriteKey(path, 0, adapterLow, sourceId);
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
        BitConverter.GetBytes(sourceId).CopyTo(modes, 68);
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

    private static WindowsDisplayService.Snapshot MultiSnapshot(
        uint adapterLow,
        uint queryFlags,
        bool available,
        params (uint SourceId, uint TargetId, string DevicePath)[] entries)
    {
        var paths = new byte[entries.Length * 72];
        var modes = new byte[entries.Length * 2 * 64];
        var targets = new List<WindowsDisplayService.Target>();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var pathOffset = index * 72;
            WriteKey(paths, pathOffset, adapterLow, entry.SourceId);
            WriteKey(paths, pathOffset + 20, adapterLow, entry.TargetId);
            BitConverter.GetBytes((uint)(index * 2 + 1)).CopyTo(paths, pathOffset + 12);
            BitConverter.GetBytes((uint)(index * 2)).CopyTo(paths, pathOffset + 32);
            BitConverter.GetBytes(available ? 1u : 0u).CopyTo(paths, pathOffset + 60);
            BitConverter.GetBytes(1u).CopyTo(paths, pathOffset + 68);

            var targetMode = index * 2 * 64;
            BitConverter.GetBytes(2u).CopyTo(modes, targetMode);
            BitConverter.GetBytes(entry.TargetId).CopyTo(modes, targetMode + 4);
            BitConverter.GetBytes(adapterLow).CopyTo(modes, targetMode + 8);

            var sourceMode = (index * 2 + 1) * 64;
            BitConverter.GetBytes(1u).CopyTo(modes, sourceMode);
            BitConverter.GetBytes(entry.SourceId).CopyTo(modes, sourceMode + 4);
            BitConverter.GetBytes(adapterLow).CopyTo(modes, sourceMode + 8);
            BitConverter.GetBytes(1920u).CopyTo(modes, sourceMode + 16);
            BitConverter.GetBytes(1080u).CopyTo(modes, sourceMode + 20);

            targets.Add(new(adapterLow, 0, entry.TargetId, entry.DevicePath, $"Display{index}", 58117, (ushort)(9986 + index), 0));
        }

        return new(
            1,
            queryFlags,
            72,
            64,
            Convert.ToBase64String(paths),
            Convert.ToBase64String(modes),
            targets);
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
    public void Rebinds_when_device_path_changes_but_edid_and_name_match()
    {
        const string newPath = @"\\?\DISPLAY#AOC2702#7&350abd5e&1&UID41015#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var journal = Snapshot(OldAdapter, 2);
        var current = Snapshot(NewAdapter, 2, targetId: 41015, devicePath: newPath);
        var available = Snapshot(NewAdapter, 1, targetId: 41015, devicePath: newPath);

        var rehydrated = DisplaySnapshotRehydrator.Rehydrate(journal, available);

        Assert.True(DisplaySnapshotComparer.SemanticallyEqual(rehydrated, current));
    }

    [Fact]
    public void Rehydrates_extend_with_distinct_sources()
    {
        const string internalPath = @"\\?\DISPLAY#AOC2702#7&350abd5e&1&UID24646#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        const string tvPath = @"\\?\DISPLAY#SAM7167#7&350abd5e&1&UID41015#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var journal = MultiSnapshot(OldAdapter, 2, true, (0, 24646, internalPath), (1, 41015, tvPath));
        var current = MultiSnapshot(NewAdapter, 2, true, (0, 24646, internalPath), (1, 41015, tvPath));
        var available = MultiSnapshot(NewAdapter, 1, true, (0, 24646, internalPath), (1, 41015, tvPath));

        var rehydrated = DisplaySnapshotRehydrator.Rehydrate(journal, available);

        Assert.True(DisplaySnapshotComparer.SemanticallyEqual(rehydrated, current));
    }

    [Fact]
    public void Rehydrates_inactive_but_connected_second_display()
    {
        const string internalPath = @"\\?\DISPLAY#AOC2702#7&350abd5e&1&UID24646#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        const string tvPath = @"\\?\DISPLAY#SAM7167#7&350abd5e&1&UID41015#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var journal = MultiSnapshot(OldAdapter, 2, true, (0, 24646, internalPath), (1, 41015, tvPath));
        var current = MultiSnapshot(NewAdapter, 2, true, (0, 24646, internalPath), (1, 41015, tvPath));
        var available = MultiSnapshot(NewAdapter, 1, true, (0, 24646, internalPath), (1, 41015, tvPath));
        var availablePaths = Convert.FromBase64String(available.PathsBase64);
        BitConverter.GetBytes(0u).CopyTo(availablePaths, 72 + 68);
        available = available with { PathsBase64 = Convert.ToBase64String(availablePaths) };

        var rehydrated = DisplaySnapshotRehydrator.Rehydrate(journal, available);

        Assert.True(DisplaySnapshotComparer.SemanticallyEqual(rehydrated, current));
    }

    [Fact]
    public void Rejects_source_collapse_between_extend_paths()
    {
        const string internalPath = @"\\?\DISPLAY#AOC2702#7&350abd5e&1&UID24646#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        const string tvPath = @"\\?\DISPLAY#SAM7167#7&350abd5e&1&UID41015#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var journal = MultiSnapshot(OldAdapter, 2, true, (0, 24646, internalPath), (1, 41015, tvPath));
        var available = MultiSnapshot(NewAdapter, 1, true, (0, 24646, internalPath), (0, 41015, tvPath));

        Assert.Throws<DisplaySnapshotRehydrationException>(() => DisplaySnapshotRehydrator.Rehydrate(journal, available));
    }

    [Fact]
    public void Rejects_ambiguous_edid_and_name_when_path_is_gone()
    {
        var journal = Snapshot(OldAdapter, 2);
        var paths = new byte[144];
        WriteKey(paths, 0, NewAdapter, 0);
        WriteKey(paths, 20, NewAdapter, 41015);
        BitConverter.GetBytes(1u).CopyTo(paths, 60);
        BitConverter.GetBytes(1u).CopyTo(paths, 68);
        WriteKey(paths, 72, NewAdapter, 0);
        WriteKey(paths, 92, NewAdapter, 41016);
        BitConverter.GetBytes(1u).CopyTo(paths, 132);
        BitConverter.GetBytes(1u).CopyTo(paths, 140);
        var available = new WindowsDisplayService.Snapshot(1, 1, 72, 64,
            Convert.ToBase64String(paths), "",
            [
                new(NewAdapter, 0, 41015, "path-a", "27G2G4", 58117, 9986, 0),
                new(NewAdapter, 0, 41016, "path-b", "27G2G4", 58117, 9986, 0),
            ]);

        Assert.Throws<DisplaySnapshotRehydrationException>(() => DisplaySnapshotRehydrator.Rehydrate(journal, available));
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
    public async Task Restore_rollback_uses_the_active_snapshot_after_verification_failure()
    {
        var journal = Snapshot(OldAdapter, 2);
        var available = Snapshot(NewAdapter, 1);
        var before = Snapshot(NewAdapter, 2, targetId: 41015, devicePath: "other");
        var active = before;
        var actions = new List<(uint Action, byte[] Paths)>();
        var service = new WindowsDisplayService(
            new("", 0, 0, AllowWrites: true),
            flags => flags == 1 ? available : active,
            (paths, _, action) => { actions.Add((action, paths.ToArray())); return 0; });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAsync(
            new DisplayState(DisplayMode.InternalOnly, true, false)
            {
                Topology = JsonSerializer.Serialize(journal),
            }));

        Assert.Equal(new uint[] { 0x40, 0x80, 0x40, 0x80 }, actions.Select(action => action.Action));
        var beforePaths = Convert.FromBase64String(before.PathsBase64);
        Assert.Equal(beforePaths, actions[2].Paths);
        Assert.Equal(beforePaths, actions[3].Paths);
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

        Assert.Throws<DisplaySnapshotRehydrationException>(() => DisplaySnapshotRehydrator.Rehydrate(journal, available));
    }

    private static void WriteKey(byte[] path, int offset, uint adapterLow, uint id)
    {
        BitConverter.GetBytes(adapterLow).CopyTo(path, offset);
        BitConverter.GetBytes(0).CopyTo(path, offset + 4);
        BitConverter.GetBytes(id).CopyTo(path, offset + 8);
    }
}

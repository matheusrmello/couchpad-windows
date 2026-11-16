using System.Buffers.Binary;

namespace CouchPad.Platform.Windows;

/// <summary>
/// Rebinds a persisted CCD snapshot to the current adapter/source identities.
/// Windows adapter LUIDs and some source ids are not stable across reboots or
/// driver resets, so a journal captured in an earlier session cannot be passed
/// to SetDisplayConfig verbatim. Targets are matched by stable identity
/// (device path, EDID and friendly name); the current path supplies the source
/// identity. The result is still validated and applied by the normal guarded
/// transaction.
/// </summary>
public static class DisplaySnapshotRehydrator
{
    private const int PathSize = 72;
    private const int ModeSize = 64;
    private const uint ActivePaths = 2;
    private const uint AllPaths = 1;
    private const uint SourceMode = 1;
    private const uint TargetMode = 2;
    private const uint DesktopImageMode = 3;

    private readonly record struct Key(uint Low, int High, uint Id);

    public static WindowsDisplayService.Snapshot Rehydrate(
        WindowsDisplayService.Snapshot expected,
        WindowsDisplayService.Snapshot available)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(available);
        if (expected.SchemaVersion != 1 || expected.QueryFlags != ActivePaths ||
            expected.PathSize != PathSize || expected.ModeSize != ModeSize)
            throw new InvalidOperationException("Unsupported display snapshot.");
        if (available.SchemaVersion != 1 || available.QueryFlags != AllPaths ||
            available.PathSize != PathSize || available.ModeSize != ModeSize)
            throw new InvalidOperationException("Unsupported available display snapshot.");

        var expectedPaths = Decode(expected.PathsBase64, PathSize, "display paths");
        var expectedModes = Decode(expected.ModesBase64, ModeSize, "display modes");
        var availablePaths = Decode(available.PathsBase64, PathSize, "available display paths");
        var availableTargets = available.Targets
            .GroupBy(KeyOf)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(target => target.Status == 0).First());
        var expectedTargets = expected.Targets
            .GroupBy(KeyOf)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(target => target.Status == 0).First());
        var currentPaths = Enumerable.Range(0, availablePaths.Length / PathSize)
            .Select(index => availablePaths.AsSpan(index * PathSize, PathSize).ToArray())
            .ToArray();

        var mappedPaths = (byte[])expectedPaths.Clone();
        var targetMap = new Dictionary<Key, Key>();
        var sourceMap = new Dictionary<Key, Key>();

        for (var offset = 0; offset < expectedPaths.Length; offset += PathSize)
        {
            var oldTarget = TargetKey(expectedPaths, offset);
            if (!targetMap.TryGetValue(oldTarget, out var newTarget))
            {
                var match = expectedTargets.TryGetValue(oldTarget, out var identity)
                    ? MatchTarget(identity, availableTargets.Values)
                    : availableTargets.GetValueOrDefault(oldTarget);
                if (match is null)
                    throw new InvalidOperationException("Recovery target is not available.");
                newTarget = KeyOf(match);
                if (targetMap.Values.Contains(newTarget))
                    throw new InvalidOperationException("Recovery target identity is ambiguous.");
                targetMap.Add(oldTarget, newTarget);
            }

            var oldSource = SourceKey(expectedPaths, offset);
            var candidates = currentPaths
                .Where(path => TargetKey(path, 0) == newTarget && ReadU32(path, 60) != 0)
                .ToArray();
            var current = candidates.FirstOrDefault(path => SourceKey(path, 0).Id == oldSource.Id)
                ?? candidates.FirstOrDefault(path => (ReadU32(path, 68) & 1) != 0)
                ?? candidates.FirstOrDefault();
            if (current is null)
                throw new InvalidOperationException("Recovery target has no available source path.");
            var newSource = SourceKey(current, 0);

            if (sourceMap.TryGetValue(oldSource, out var existing) && existing != newSource)
                throw new InvalidOperationException("Recovery source identity is ambiguous.");
            if (sourceMap.Any(pair => pair.Key != oldSource && pair.Value == newSource))
                throw new InvalidOperationException("Recovery source identities are ambiguous.");
            sourceMap[oldSource] = newSource;

            WritePathKey(mappedPaths, offset, newSource);
            WritePathKey(mappedPaths, offset + 20, newTarget);
        }

        var mappedModes = (byte[])expectedModes.Clone();
        for (var offset = 0; offset < expectedModes.Length; offset += ModeSize)
        {
            var type = ReadU32(expectedModes, offset);
            var oldKey = new Key(ReadU32(expectedModes, offset + 8), ReadI32(expectedModes, offset + 12), ReadU32(expectedModes, offset + 4));
            Key newKey;
            if (type is SourceMode or DesktopImageMode)
            {
                if (!sourceMap.TryGetValue(oldKey, out newKey))
                    throw new InvalidOperationException("Recovery source mode is not referenced by a path.");
            }
            else if (type == TargetMode)
            {
                if (!targetMap.TryGetValue(oldKey, out newKey))
                    throw new InvalidOperationException("Recovery target mode is not referenced by a path.");
            }
            else
            {
                throw new InvalidOperationException("Unsupported display mode record.");
            }

            WriteModeKey(mappedModes, offset, newKey);
        }

        var mappedTargets = targetMap.Values
            .Distinct()
            .Select(key => availableTargets.TryGetValue(key, out var target)
                ? target
                : throw new InvalidOperationException("Recovery target metadata is unavailable."))
            .ToList();

        return expected with
        {
            PathsBase64 = Convert.ToBase64String(mappedPaths),
            ModesBase64 = Convert.ToBase64String(mappedModes),
            Targets = mappedTargets,
        };
    }

    private static WindowsDisplayService.Target? MatchTarget(
        WindowsDisplayService.Target expected,
        IEnumerable<WindowsDisplayService.Target> available)
    {
        var matches = available.Where(candidate => StableIdentityMatches(expected, candidate)).ToArray();
        if (matches.Length == 0)
        {
            var byKey = available.Where(candidate => KeyOf(candidate) == KeyOf(expected)).ToArray();
            return byKey.Length switch
            {
                0 => null,
                1 => byKey[0],
                _ => throw new InvalidOperationException("Recovery target identity is ambiguous."),
            };
        }
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("Recovery target identity is ambiguous.");
    }

    private static bool StableIdentityMatches(
        WindowsDisplayService.Target expected,
        WindowsDisplayService.Target candidate)
    {
        if (!string.IsNullOrWhiteSpace(expected.DevicePath))
        {
            if (!string.Equals(expected.DevicePath, candidate.DevicePath, StringComparison.OrdinalIgnoreCase))
                return false;
            if (expected.EdidManufacturer is { } manufacturer && candidate.EdidManufacturer != manufacturer)
                return false;
            if (expected.EdidProduct is { } product && candidate.EdidProduct != product)
                return false;
            return true;
        }

        if (expected.EdidManufacturer is { } expectedManufacturer &&
            expected.EdidProduct is { } expectedProduct)
        {
            if (candidate.EdidManufacturer != expectedManufacturer || candidate.EdidProduct != expectedProduct)
                return false;
            return string.IsNullOrWhiteSpace(expected.FriendlyName) ||
                string.Equals(expected.FriendlyName, candidate.FriendlyName, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(expected.FriendlyName) &&
            string.Equals(expected.FriendlyName, candidate.FriendlyName, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Decode(string value, int size, string name)
    {
        var bytes = Convert.FromBase64String(value);
        if (bytes.Length == 0 || bytes.Length % size != 0)
            throw new InvalidOperationException($"Invalid {name}.");
        return bytes;
    }

    private static Key KeyOf(WindowsDisplayService.Target target) => new(target.AdapterLow, target.AdapterHigh, target.Id);

    private static Key SourceKey(byte[] path, int offset) =>
        new(ReadU32(path, offset), ReadI32(path, offset + 4), ReadU32(path, offset + 8));

    private static Key TargetKey(byte[] path, int offset) =>
        new(ReadU32(path, offset + 20), ReadI32(path, offset + 24), ReadU32(path, offset + 28));

    private static void WritePathKey(byte[] path, int offset, Key key)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(path.AsSpan(offset), key.Low);
        BinaryPrimitives.WriteInt32LittleEndian(path.AsSpan(offset + 4), key.High);
        BinaryPrimitives.WriteUInt32LittleEndian(path.AsSpan(offset + 8), key.Id);
    }

    private static void WriteModeKey(byte[] mode, int offset, Key key)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(offset + 8), key.Low);
        BinaryPrimitives.WriteInt32LittleEndian(mode.AsSpan(offset + 12), key.High);
        BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(offset + 4), key.Id);
    }

    private static uint ReadU32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static int ReadI32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}

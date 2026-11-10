using System.Buffers.Binary;
using CouchPad.Core.Display;

namespace CouchPad.Platform.Windows;

/// <summary>Builds an unvalidated CCD path proposal; never calls Windows or changes a display.</summary>
public static class DisplayTopologyPlanner
{
    private const int PathSize = 72;
    private const uint InvalidIndex = uint.MaxValue;
    private const uint Active = 1;
    private readonly record struct Source(uint Low, int High, uint Id);
    private readonly record struct TargetKey(uint Low, int High, uint Id);
    public sealed record Proposal(DisplayMode Mode, byte[] Paths);

    public static Proposal Create(WindowsDisplayService.Snapshot baseline, WindowsDisplayService.Snapshot available, WindowsDisplayService.Target tv, WindowsDisplayService.Target internalDisplay, DisplayMode mode)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(tv);
        ArgumentNullException.ThrowIfNull(internalDisplay);
        if (mode is not (DisplayMode.TvOnly or DisplayMode.Extend or DisplayMode.Mirror))
            throw new ArgumentOutOfRangeException(nameof(mode));
        var current = ReadPaths(baseline, 2);
        var candidates = ReadPaths(available, 1);
        var tvKey = new TargetKey(tv.AdapterLow, tv.AdapterHigh, tv.Id);
        var internalKey = new TargetKey(internalDisplay.AdapterLow, internalDisplay.AdapterHigh, internalDisplay.Id);
        if (internalKey == tvKey) throw new InvalidOperationException("Display identities must differ.");
        if (current.Any(p => TargetOf(p) != internalKey && TargetOf(p) != tvKey))
            throw new InvalidOperationException("Unexpected active display.");
        var internalCandidates = candidates.Where(p => TargetOf(p) == internalKey && Read(p, 60) != 0).ToArray();
        var tvCandidates = candidates.Where(p => TargetOf(p) == tvKey && Read(p, 60) != 0).ToArray();
        if (internalCandidates.Length == 0 || tvCandidates.Length == 0) throw new InvalidOperationException("Selected display is unavailable.");
        // Prefer the currently active source for each selected target, then CCD enumeration order.
        var activeInternal = current.FirstOrDefault(p => TargetOf(p) == internalKey);
        var activeTv = current.FirstOrDefault(p => TargetOf(p) == tvKey);
        var orderedInternal = internalCandidates.OrderByDescending(p => activeInternal is not null && SourceOf(p) == SourceOf(activeInternal)).ToArray();
        var orderedTv = tvCandidates.OrderByDescending(p => activeTv is not null && SourceOf(p) == SourceOf(activeTv)).ToArray();
        byte[][] selected;
        if (mode == DisplayMode.TvOnly)
        {
            selected = [orderedTv[0]];
        }
        else
        {
            var pairs = from primary in orderedInternal from target in orderedTv
                where (SourceOf(primary) == SourceOf(target)) == (mode == DisplayMode.Mirror)
                select new[] { primary, target };
            selected = pairs.FirstOrDefault() ?? throw new InvalidOperationException("No available path pair for this topology.");
        }
        var output = new byte[selected.Length * PathSize];
        for (var i = 0; i < selected.Length; i++)
        {
            var path = selected[i].AsSpan().ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(path.AsSpan(12), InvalidIndex);
            BinaryPrimitives.WriteUInt32LittleEndian(path.AsSpan(32), InvalidIndex);
            BinaryPrimitives.WriteUInt32LittleEndian(path.AsSpan(68), Read(path, 68) | Active);
            path.CopyTo(output, i * PathSize);
        }
        return new Proposal(mode, output);
    }

    private static byte[][] ReadPaths(WindowsDisplayService.Snapshot snapshot, uint flags)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.QueryFlags != flags || snapshot.PathSize != PathSize || snapshot.ModeSize != 64)
            throw new InvalidOperationException("Unsupported display snapshot.");
        var bytes = Convert.FromBase64String(snapshot.PathsBase64);
        if (bytes.Length == 0 || bytes.Length % PathSize != 0) throw new InvalidOperationException("Invalid display paths.");
        return Enumerable.Range(0, bytes.Length / PathSize).Select(i => bytes.AsSpan(i * PathSize, PathSize).ToArray()).ToArray();
    }
    private static uint Read(byte[] path, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(path.AsSpan(offset));
    private static Source SourceOf(byte[] path) => new(Read(path, 0), unchecked((int)Read(path, 4)), Read(path, 8));
    private static TargetKey TargetOf(byte[] path) => new(Read(path, 20), unchecked((int)Read(path, 24)), Read(path, 28));
}


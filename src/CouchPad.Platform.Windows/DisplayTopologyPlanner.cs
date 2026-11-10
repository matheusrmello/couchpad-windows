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

    public static Proposal Create(WindowsDisplayService.Snapshot baseline, WindowsDisplayService.Snapshot available, WindowsDisplayService.Target tv, DisplayMode mode)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(tv);
        if (mode is not (DisplayMode.TvOnly or DisplayMode.Extend or DisplayMode.Mirror))
            throw new ArgumentOutOfRangeException(nameof(mode));
        var current = ReadPaths(baseline, 2);
        var candidates = ReadPaths(available, 1);
        var tvKey = new TargetKey(tv.AdapterLow, tv.AdapterHigh, tv.Id);
        var internalPaths = current.Where(p => TargetOf(p) != tvKey).ToArray();
        if (internalPaths.Length != 1 || current.Count(p => TargetOf(p) == tvKey) > 1)
            throw new InvalidOperationException("Exactly one active non-TV display is required.");
        var internalPath = internalPaths[0];
        var source = SourceOf(internalPath);
        var possible = candidates.Where(p => TargetOf(p) == tvKey && Read(p, 60) != 0)
            .Where(p => mode == DisplayMode.Extend ? SourceOf(p) != source : SourceOf(p) == source)
            .ToArray();
        if (possible.Length == 0)
            throw new InvalidOperationException("No available TV path for this topology was found.");
        var selected = mode == DisplayMode.TvOnly ? new[] { possible[0] } : new[] { internalPath, possible[0] };
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


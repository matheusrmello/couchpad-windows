using System.Buffers.Binary;

namespace CouchPad.Platform.Windows;

/// <summary>Compares active topology identities and explicit modes, ignoring CCD status and array ordering.</summary>
public static class DisplaySnapshotComparer
{
    public static bool SemanticallyEqual(WindowsDisplayService.Snapshot expected, WindowsDisplayService.Snapshot actual) =>
        Describe(expected).SequenceEqual(Describe(actual), StringComparer.Ordinal);

    private static string[] Describe(WindowsDisplayService.Snapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.QueryFlags != 2 || snapshot.PathSize != 72 || snapshot.ModeSize != 64)
            throw new InvalidOperationException("Unsupported display snapshot.");
        var paths = Convert.FromBase64String(snapshot.PathsBase64);
        var modes = Convert.FromBase64String(snapshot.ModesBase64);
        if (paths.Length == 0 || paths.Length % 72 != 0 || modes.Length == 0 || modes.Length % 64 != 0)
            throw new InvalidOperationException("Invalid display snapshot.");
        var result = new List<string>();
        for (var p = 0; p < paths.Length; p += 72)
        {
            if ((U32(paths, p + 68) & 1) == 0) throw new InvalidOperationException("Snapshot contains inactive path.");
            var sourceIndex = U32(paths, p + 12);
            var targetIndex = U32(paths, p + 32);
            var src = Mode(modes, sourceIndex, 1, paths, p, 0);
            var dst = Mode(modes, targetIndex, 2, paths, p, 20);
            result.Add($"{U32(paths,p):X8}:{U32(paths,p+4):X8}:{U32(paths,p+8):X8}>{U32(paths,p+20):X8}:{U32(paths,p+24):X8}:{U32(paths,p+28):X8}|{U32(paths,p+40)}:{U32(paths,p+44)}|{src}|{dst}");
        }
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    private static string Mode(byte[] modes, uint index, uint type, byte[] paths, int p, int idOffset)
    {
        if (index == uint.MaxValue || index >= modes.Length / 64) throw new InvalidOperationException("Missing mode information.");
        var offset = checked((int)index * 64);
        if (U32(modes, offset) != type || U32(modes, offset + 4) != U32(paths, p + idOffset + 8) ||
            U32(modes, offset + 8) != U32(paths, p + idOffset) || U32(modes, offset + 12) != U32(paths, p + idOffset + 4))
            throw new InvalidOperationException("Mode identity does not match path.");
        // Source: dimensions, pixel format, position. Target: signal timing and dimensions.
        var count = type == 1 ? 5 : 12;
        return string.Join(':', Enumerable.Range(0, count).Select(i => U32(modes, offset + 16 + i * 4).ToString("X8")));
    }
    private static uint U32(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset, 4));
}

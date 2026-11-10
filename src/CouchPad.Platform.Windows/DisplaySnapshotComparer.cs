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
        if (type == 1)
            return string.Join(':', Enumerable.Range(0, 5).Select(i => U32(modes, offset + 16 + i * 4).ToString("X8")));

        // Target mode supplies the effective sync rates and scan-line ordering;
        // represent equivalent rational frequencies identically and ignore the
        // reserved high bits of AdditionalSignalInfo.
        return string.Join(':',
            U32(modes, offset + 16).ToString("X8"), U32(modes, offset + 20).ToString("X8"),
            Rational(U32(modes, offset + 24), U32(modes, offset + 28)),
            Rational(U32(modes, offset + 32), U32(modes, offset + 36)),
            U32(modes, offset + 40).ToString("X8"), U32(modes, offset + 44).ToString("X8"),
            U32(modes, offset + 48).ToString("X8"), U32(modes, offset + 52).ToString("X8"),
            (U32(modes, offset + 56) & 0x003fffff).ToString("X8"), U32(modes, offset + 60).ToString("X8"));
    }

    private static string Rational(uint numerator, uint denominator)
    {
        if (denominator == 0) return $"{numerator}/0";
        var a = numerator;
        var b = denominator;
        while (b != 0) (a, b) = (b, a % b);
        return $"{numerator / a}/{denominator / a}";
    }
    private static uint U32(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset, 4));
}

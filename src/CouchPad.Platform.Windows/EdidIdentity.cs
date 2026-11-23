using System.Buffers.Binary;
using System.Text;

namespace CouchPad.Platform.Windows;

/// <summary>
/// Extracts the identity text stored in the EDID base block: the monitor name,
/// the serial string and the Windows per-monitor DPI prefix
/// (<c>MFG PRODUCT SERIAL</c>). Pure code — no registry, no display writes.
/// </summary>
public static class EdidIdentity
{
    private const int BlockSize = 128;
    private const int DescriptorCount = 4;
    private const int FirstDescriptorOffset = 54;
    private const int DescriptorSize = 18;
    private const int DescriptorTextOffset = 5;
    private const int DescriptorTextLength = 13;
    private const int SerialStringTag = 0xFF;
    private const int MonitorNameTag = 0xFC;
    private const int ManufacturerOffset = 8;
    private const int ProductOffset = 10;
    private const int ProductHexDigits = 4;

    private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    /// <summary>
    /// Returns the serial string descriptor (<c>0xFF</c>) trimmed, or
    /// <see langword="null"/> when the block is malformed or has no serial.
    /// </summary>
    public static string? ReadSerialString(ReadOnlySpan<byte> edid) => ReadDescriptorText(edid, SerialStringTag);

    /// <summary>
    /// Returns the monitor name descriptor (<c>0xFC</c>) trimmed, or
    /// <see langword="null"/> when the block is malformed or has no name.
    /// </summary>
    public static string? ReadMonitorName(ReadOnlySpan<byte> edid) => ReadDescriptorText(edid, MonitorNameTag);

    /// <summary>
    /// Builds the uppercase <c>MFG PRODUCT SERIAL</c> prefix used by Windows to
    /// name the <c>PerMonitorSettings</c> subkey (for example
    /// <c>SAM716716780800</c>). Falls back to the monitor name when the serial
    /// is absent; returns <see langword="null"/> when the identity is incomplete.
    /// </summary>
    public static string? BuildDpiPrefix(ReadOnlySpan<byte> edid)
    {
        if (!HasValidHeader(edid))
            return null;

        var manufacturer = ReadManufacturer(edid);
        if (manufacturer is null)
            return null;

        var serial = ReadSerialString(edid) ?? ReadMonitorName(edid);
        if (string.IsNullOrEmpty(serial))
            return null;

        var product = BinaryPrimitives.ReadUInt16LittleEndian(edid.Slice(ProductOffset, 2));
        return string.Concat(manufacturer, product.ToString($"X{ProductHexDigits}"), serial).ToUpperInvariant();
    }

    private static string? ReadManufacturer(ReadOnlySpan<byte> edid)
    {
        if (!HasValidHeader(edid))
            return null;

        var word = BinaryPrimitives.ReadUInt16BigEndian(edid.Slice(ManufacturerOffset, 2));
        Span<char> letters = stackalloc char[3];
        for (var index = 0; index < letters.Length; index++)
        {
            var value = (word >> ((letters.Length - 1 - index) * 5)) & 0x1F;
            if (value is < 1 or > 26)
                return null;
            letters[index] = (char)('A' + value - 1);
        }

        return new string(letters);
    }

    private static string? ReadDescriptorText(ReadOnlySpan<byte> edid, int tag)
    {
        if (!HasValidHeader(edid))
            return null;

        for (var index = 0; index < DescriptorCount; index++)
        {
            var descriptor = edid.Slice(FirstDescriptorOffset + (index * DescriptorSize), DescriptorSize);
            if (BinaryPrimitives.ReadUInt16LittleEndian(descriptor) != 0 || descriptor[3] != tag)
                continue;

            var text = Encoding.ASCII.GetString(descriptor.Slice(DescriptorTextOffset, DescriptorTextLength));
            var value = text.Trim(' ', '\n', '\r', '\0');
            return value.Length == 0 ? null : value;
        }

        return null;
    }

    private static bool HasValidHeader(ReadOnlySpan<byte> edid) =>
        edid.Length >= BlockSize && edid[..Header.Length].SequenceEqual(Header);
}

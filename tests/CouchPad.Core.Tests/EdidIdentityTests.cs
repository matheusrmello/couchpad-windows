using System.Text;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class EdidIdentityTests
{
    private static readonly byte[] EdidHeader = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    [Fact]
    public void Dpi_prefix_uses_manufacturer_product_and_serial_descriptor()
    {
        var edid = CreateEdid(serial: "16780800", monitorName: "SAMSUNG");

        Assert.Equal("SAM716716780800", EdidIdentity.BuildDpiPrefix(edid));
    }

    [Fact]
    public void Dpi_prefix_falls_back_to_monitor_name_without_serial()
    {
        var edid = CreateEdid(serial: null, monitorName: "SAMSUNG");

        Assert.Equal("SAM7167SAMSUNG", EdidIdentity.BuildDpiPrefix(edid));
    }

    [Fact]
    public void Dpi_prefix_is_null_when_identity_text_is_missing()
    {
        Assert.Null(EdidIdentity.BuildDpiPrefix(CreateEdid(serial: null, monitorName: null)));
    }

    [Fact]
    public void Short_or_invalid_header_yields_null()
    {
        Assert.Null(EdidIdentity.BuildDpiPrefix(ReadOnlySpan<byte>.Empty));
        Assert.Null(EdidIdentity.BuildDpiPrefix(new byte[100]));

        var badHeader = CreateEdid(serial: "16780800", monitorName: null);
        badHeader[7] = 0x01;

        Assert.Null(EdidIdentity.BuildDpiPrefix(badHeader));
    }

    [Fact]
    public void Descriptor_readers_return_trimmed_text()
    {
        var edid = CreateEdid(serial: "16780800", monitorName: "SAMSUNG");

        Assert.Equal("16780800", EdidIdentity.ReadSerialString(edid));
        Assert.Equal("SAMSUNG", EdidIdentity.ReadMonitorName(edid));
    }

    private static byte[] CreateEdid(string? serial, string? monitorName)
    {
        var edid = new byte[128];
        EdidHeader.CopyTo(edid, 0);

        // Manufacturer "SAM" encodes as 0x4C2D (big-endian), product 0x7167
        // stored little-endian — the identity Windows uses for PerMonitorSettings.
        edid[8] = 0x4C;
        edid[9] = 0x2D;
        edid[10] = 0x67;
        edid[11] = 0x71;

        var slot = 0;
        if (serial is not null)
            WriteDescriptor(edid, slot++, 0xFF, serial);
        if (monitorName is not null)
            WriteDescriptor(edid, slot, 0xFC, monitorName);

        return edid;
    }

    private static void WriteDescriptor(byte[] edid, int slot, byte tag, string text)
    {
        var descriptor = edid.AsSpan(54 + (slot * 18), 18);
        descriptor.Clear();
        descriptor[3] = tag;
        Encoding.ASCII.GetBytes(text).CopyTo(descriptor[5..]);
    }
}

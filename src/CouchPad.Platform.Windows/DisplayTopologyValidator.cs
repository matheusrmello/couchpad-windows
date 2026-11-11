using System.Runtime.InteropServices;
namespace CouchPad.Platform.Windows;

/// <summary>Valida propostas CCD sem aplicar alterações.</summary>
public static class DisplayTopologyValidator
{
    private const uint Validate = 0x40, SuppliedConfig = 0x20;
    public static int ValidateProposal(DisplayTopologyPlanner.Proposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (proposal.Paths.Length == 0 || proposal.Paths.Length % 72 != 0) throw new ArgumentException("Invalid path array.", nameof(proposal));
        var handle = GCHandle.Alloc(proposal.Paths, GCHandleType.Pinned);
        try { return SetDisplayConfig((uint)(proposal.Paths.Length / 72), handle.AddrOfPinnedObject(), 0, IntPtr.Zero, Validate | SuppliedConfig); }
        finally { handle.Free(); }
    }
    public static int ValidateSnapshot(WindowsDisplayService.Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var paths = Convert.FromBase64String(snapshot.PathsBase64);
        var modes = Convert.FromBase64String(snapshot.ModesBase64);
        if (snapshot.SchemaVersion != 1 || snapshot.PathSize != 72 || snapshot.ModeSize != 64 ||
            paths.Length == 0 || paths.Length % 72 != 0 || modes.Length == 0 || modes.Length % 64 != 0)
            throw new ArgumentException("Invalid snapshot.", nameof(snapshot));
        var pathHandle = GCHandle.Alloc(paths, GCHandleType.Pinned);
        var modeHandle = GCHandle.Alloc(modes, GCHandleType.Pinned);
        try { return SetDisplayConfig((uint)(paths.Length / 72), pathHandle.AddrOfPinnedObject(), (uint)(modes.Length / 64), modeHandle.AddrOfPinnedObject(), Validate | SuppliedConfig); }
        finally { modeHandle.Free(); pathHandle.Free(); }
    }
    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(uint pathCount, IntPtr paths, uint modeCount, IntPtr modes, uint flags);
}

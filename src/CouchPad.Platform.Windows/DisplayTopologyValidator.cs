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
    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(uint pathCount, IntPtr paths, uint modeCount, IntPtr modes, uint flags);
}

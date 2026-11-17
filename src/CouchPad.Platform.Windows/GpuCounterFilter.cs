namespace CouchPad.Platform.Windows;

/// <summary>
/// Matches PDH "GPU Engine" instances to one process and its 3D engine.
/// Instance names look like <c>pid_5360_luid_0x0000e695_phys_0_eng_0_engtype_3D</c>.
/// </summary>
public static class GpuCounterFilter
{
    public static bool IsProcess3D(string? instanceName, int processId)
    {
        if (string.IsNullOrEmpty(instanceName) || processId <= 0) return false;
        return instanceName.Contains($"pid_{processId}_", StringComparison.OrdinalIgnoreCase) &&
            instanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase);
    }
}

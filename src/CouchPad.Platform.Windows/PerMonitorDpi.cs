using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CouchPad.Platform.Windows;

/// <summary>
/// Reads and writes the per-monitor DPI scale that Windows keeps under
/// <c>HKCU\Control Panel\Desktop\PerMonitorSettings</c>. The subkey name is a
/// hash of the monitor identity, so it cannot be created here: callers must
/// find an existing key. Non-Windows and any registry error are fail-safe
/// (<see langword="null"/> or <see langword="false"/>), never an exception.
/// </summary>
public static class PerMonitorDpi
{
    public const string SettingsPath = @"Control Panel\Desktop\PerMonitorSettings";

    private const string DesktopPath = @"Control Panel\Desktop";
    private const string DpiValueName = "DpiValue";
    private const string Win8DpiScalingName = "Win8DpiScaling";
    private const int RecommendedDpiValue = -1;
    private const int FirstCustomDpiValue = 0;
    private const int LastCustomDpiValue = 4;

    private const int PercentStep = 25;
    private const int BasePercent = 100;

    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint BroadcastTimeoutMs = 1000;
    private static readonly IntPtr HwndBroadcast = new(0xFFFF);
    private const string WindowMetricsSetting = "WindowMetrics";

    /// <summary>
    /// Returns the first <c>PerMonitorSettings</c> subkey that starts with
    /// <paramref name="prefix"/> (the EDID identity), or <see langword="null"/>
    /// when Windows has never stored a DPI profile for the monitor.
    /// </summary>
    public static string? FindKey(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !OperatingSystem.IsWindows())
            return null;

        try
        {
            using var settings = Registry.CurrentUser.OpenSubKey(SettingsPath, writable: false);
            if (settings is null)
                return null;

            foreach (var name in settings.GetSubKeyNames())
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Reads the stored scale as a percentage (100..200), or <see langword="null"/>.</summary>
    public static int? ReadPercent(string key) =>
        ReadDpiValue(key) is { } value ? FromDpiValue(value) : null;

    /// <summary>
    /// Stores <paramref name="percent"/> as <c>DpiValue</c>, forces
    /// <c>Win8DpiScaling=1</c> and broadcasts <c>WM_SETTINGCHANGE</c>. Returns
    /// <see langword="false"/> for an unknown scale or any registry failure.
    /// The change may only take effect for apps started afterwards or after a
    /// sign-out.
    /// </summary>
    public static bool Apply(string key, int percent)
    {
        if (string.IsNullOrWhiteSpace(key) || !OperatingSystem.IsWindows())
            return false;

        var dpiValue = ToDpiValue(percent);
        if (dpiValue < 0)
            return false;

        try
        {
            if (!TryWriteDpiValue(key, dpiValue))
                return false;

            using var desktop = Registry.CurrentUser.OpenSubKey(DesktopPath, writable: true);
            if (desktop is null)
                return false;
            desktop.SetValue(Win8DpiScalingName, 1, RegistryValueKind.DWord);

            BroadcastSettingChange();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Maps a supported percentage to the Windows <c>DpiValue</c>; unknown scales yield -1.</summary>
    public static int ToDpiValue(int percent) => percent switch
    {
        BasePercent => 0,
        BasePercent + PercentStep => 1,
        BasePercent + (PercentStep * 2) => 2,
        BasePercent + (PercentStep * 3) => 3,
        BasePercent + (PercentStep * 4) => 4,
        _ => -1,
    };

    /// <summary>
    /// Maps a Windows <c>DpiValue</c> to a percentage; -1 (recommended) and any
    /// value outside 0..4 yield <see langword="null"/>.
    /// </summary>
    public static int? FromDpiValue(int value) => value switch
    {
        FirstCustomDpiValue => BasePercent,
        FirstCustomDpiValue + 1 => BasePercent + PercentStep,
        FirstCustomDpiValue + 2 => BasePercent + (PercentStep * 2),
        FirstCustomDpiValue + 3 => BasePercent + (PercentStep * 3),
        LastCustomDpiValue => BasePercent + (PercentStep * 4),
        _ => null,
    };

    /// <summary>Reads the raw <c>DpiValue</c> (-1 means "recommended"), or null when absent.</summary>
    internal static int? ReadDpiValue(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !OperatingSystem.IsWindows())
            return null;

        try
        {
            using var settings = Registry.CurrentUser.OpenSubKey($@"{SettingsPath}\{key}", writable: false);
            return settings?.GetValue(DpiValueName) switch
            {
                int value => value,
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Writes the raw <c>DpiValue</c> and broadcasts the change; used to undo an apply.</summary>
    internal static bool WriteDpiValue(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key) || !OperatingSystem.IsWindows())
            return false;

        try
        {
            if (!TryWriteDpiValue(key, value))
                return false;

            BroadcastSettingChange();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryWriteDpiValue(string key, int value)
    {
        using var settings = Registry.CurrentUser.OpenSubKey($@"{SettingsPath}\{key}", writable: true);
        if (settings is null)
            return false;

        settings.SetValue(DpiValueName, value, RegistryValueKind.DWord);
        return true;
    }

    private static void BroadcastSettingChange() => SendMessageTimeout(
        HwndBroadcast, WmSettingChange, IntPtr.Zero, WindowMetricsSetting, SmtoAbortIfHung, BroadcastTimeoutMs, out _);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}

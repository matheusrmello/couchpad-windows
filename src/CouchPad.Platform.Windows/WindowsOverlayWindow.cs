using System.Runtime.InteropServices;

namespace CouchPad.Platform.Windows;

/// <summary>Win32 styles for a non-activating, click-through HUD window.</summary>
public static class WindowsOverlayWindow
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExLayered = 0x00080000;
    private const long WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public static void ApplyClickThrough(IntPtr handle)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("A valid window handle is required.", nameof(handle));
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        style |= WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate;
        _ = SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
        _ = SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

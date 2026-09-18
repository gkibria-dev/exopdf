using System.Runtime.InteropServices;

namespace ExoPdf.Desktop.Services;

/// <summary>
/// Switches a window's native title bar between light and dark using the Desktop
/// Window Manager. WPF's Fluent theme darkens the window contents but, on Windows 10,
/// leaves the title bar light.
/// </summary>
internal static class DwmTitleBar
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 from Windows 10 build 18985, 19 on 1809 to 1909.
    private static readonly int[] ImmersiveDarkModeAttributes = [20, 19];

    private const uint WmNcActivate = 0x0086;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    /// <summary>Returns false if the system does not support it; the title bar then simply stays as it was.</summary>
    public static bool SetDark(IntPtr windowHandle, bool dark)
    {
        var value = dark ? 1 : 0;

        foreach (var attribute in ImmersiveDarkModeAttributes)
        {
            if (DwmGetWindowAttribute(windowHandle, attribute, out var current, sizeof(int)) == 0 && current == value)
                return true; // already right: nothing to change, nothing to repaint

            if (DwmSetWindowAttribute(windowHandle, attribute, ref value, sizeof(int)) == 0)
            {
                Repaint(windowHandle);
                return true;
            }
        }

        return false;
    }

    // Windows only redraws the title bar when its active state changes (checked on Windows 10
    // build 19045: RedrawWindow and SWP_FRAMECHANGED do not do it). Flip the state and put
    // it back, so the window ends up exactly as active as it was.
    private static void Repaint(IntPtr windowHandle)
    {
        var active = GetForegroundWindow() == windowHandle;

        SendMessage(windowHandle, WmNcActivate, active ? IntPtr.Zero : 1, IntPtr.Zero);
        SendMessage(windowHandle, WmNcActivate, active ? 1 : IntPtr.Zero, IntPtr.Zero);
    }
}

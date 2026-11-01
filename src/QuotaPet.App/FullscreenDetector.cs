using System.Runtime.InteropServices;

namespace QuotaPet.App;

internal static class FullscreenDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    public static bool IsForegroundFullscreen(IntPtr ownWindow)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == ownWindow || !GetWindowRect(foreground, out var rect)) return false;
        var screen = Screen.FromHandle(foreground).Bounds;
        return rect.Left <= screen.Left && rect.Top <= screen.Top &&
               rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
    }
}

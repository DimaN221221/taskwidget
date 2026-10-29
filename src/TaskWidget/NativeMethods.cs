using System.Runtime.InteropServices;

namespace TaskWidget;

internal static class NativeMethods
{
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;
    private const int VK_LBUTTON = 0x01;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Start a native window move, as if the user grabbed a real title bar.</summary>
    public static void BeginWindowDrag(IntPtr hwnd)
    {
        // The page's message arrives asynchronously; skip if the button was already released,
        // otherwise Windows would start a move that follows the cursor without a click.
        if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0) return;
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
    }

    public static void Activate(IntPtr hwnd)
    {
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
    }
}

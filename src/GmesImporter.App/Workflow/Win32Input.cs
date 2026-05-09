using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GmesImporter.App.Configuration;

namespace GmesImporter.App.Workflow;

public static class Win32Input
{
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    public static void FocusWindow(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        var process = processes.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
        
        if (process != null)
        {
            ShowWindow(process.MainWindowHandle, SW_RESTORE);
            Thread.Sleep(150);
            SetForegroundWindow(process.MainWindowHandle);
            Thread.Sleep(150);
        }
    }

    public static void ClickLeft(PointOptions point, int delayAfterMs = 500)
    {
        SetCursorPos(point.X, point.Y);
        Thread.Sleep(300);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(100);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(delayAfterMs);
    }

    public static void ClickRight(PointOptions point, int delayAfterMs = 500)
    {
        SetCursorPos(point.X, point.Y);
        Thread.Sleep(300);
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(100);
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(delayAfterMs);
    }

    public static void SendKeysWait(string keys, int delayAfterMs = 500)
    {
        SendKeys.SendWait(keys);
        Thread.Sleep(delayAfterMs);
    }
}

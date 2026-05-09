using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GmesImporter.App.Security;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;

namespace GmesImporter.App.Workflow;

public static class GmesLoginKeyboardFallback
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004, MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_MOVE = 0x0001
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int SW_RESTORE = 9;
    private const int SW_MAXIMIZE = 3;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP   = 0x0004;

    public static async Task ExecuteAsync(
        string userId,
        string password,
        ILogger logger,
        IWebDriver driver,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Using keyboard fallback for GMES login screen.");

        // Click vào window title bar để bring IE to foreground
        FocusDriverWindowByClick(logger);
        await DelayAsync(cancellationToken);

        // Lúc này do click vào vùng màu đen, trang GMES đã auto-focus vào ô Language dropdown
        // Đổi language: {DOWN} trực tiếp trên select thay đổi giá trị ngay (Korean → English)
        SendKeys.SendWait("{DOWN}");
        await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken); // chờ trang re-render sau khi đổi ngôn ngữ

        logger.LogInformation("Language changed. Entering GMES credentials via keyboard.");

        // Tab tới GMES ID field
        SendKeys.SendWait("{TAB}");
        await DelayAsync(cancellationToken);
        SendKeys.SendWait(EscapeForSendKeys(SecretProtector.UnprotectConfiguredValue(userId)));
        await DelayAsync(cancellationToken);

        // Tab tới Password field
        SendKeys.SendWait("{TAB}");
        await DelayAsync(cancellationToken);
        SendKeys.SendWait(EscapeForSendKeys(SecretProtector.UnprotectConfiguredValue(password)));
        await DelayAsync(cancellationToken);

        // Submit
        SendKeys.SendWait("{ENTER}");
        await DelayAsync(cancellationToken);
    }

    private static void FocusDriverWindowByClick(ILogger logger)
    {
        try
        {
            // Tránh dùng driver.CurrentWindowHandle vì IEDriver thường treo 180s ở bước này
            // Thay vào đó, tìm process msedge (vì đang chạy AttachToEdgeChrome)
            var edgeProcess = System.Diagnostics.Process.GetProcessesByName("msedge")
                .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

            if (edgeProcess != null)
            {
                var hwnd = edgeProcess.MainWindowHandle;
                ShowWindow(hwnd, SW_MAXIMIZE);
                SetForegroundWindow(hwnd);

                if (GetWindowRect(hwnd, out var rect))
                {
                    // Theo phát hiện của user: Click vùng màu đen -> tự focus ô ngôn ngữ
                    int clickX = rect.Left + 50;
                    int clickY = rect.Top + 200; 
                    SetCursorPos(clickX, clickY);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, clickX, clickY, 0, IntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP,   clickX, clickY, 0, IntPtr.Zero);
                    logger.LogDebug("Clicked black background at ({X}, {Y}) to focus language dropdown.", clickX, clickY);
                }
            }
            else
            {
                logger.LogWarning("Could not find msedge process with a window handle.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not focus browser window before SendKeys; proceeding anyway.");
        }
    }

    public static string EscapeForSendKeys(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '+' => "{+}",
                '^' => "{^}",
                '%' => "{%}",
                '~' => "{~}",
                '(' => "{(}",
                ')' => "{)}",
                '[' => "{[}",
                ']' => "{]}",
                '{' => "{{}",
                '}' => "{}}",
                _ => character.ToString()
            });
        }

        return builder.ToString();
    }

    private static Task DelayAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
    }
}

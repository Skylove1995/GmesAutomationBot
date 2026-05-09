using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GmesImporter.App.Configuration;
using GmesImporter.App.Files;
using GmesImporter.App.Security;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.IE;

namespace GmesImporter.App.Workflow;

public sealed class GmesBrowserAutomation
{
    private readonly AppSettings _settings;
    private readonly ILogger<GmesBrowserAutomation> _logger;

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;       // Restore khi window đang minimized
    private const int SW_SHOW = 5;          // Hiển thị đúng trạng thái hiện tại (không đổi size)

    // Kiểm tra window có đang bị minimize không (iconic state)
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    // ── Force Screen Resolution (Option B: headless / no monitor) ──────────────
    [DllImport("user32.dll")]
    private static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int CDS_UPDATEREGISTRY = 0x01;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY;
        public int dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight;
        public int dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    /// <summary>
    /// Ép màn hình về resolution chỉ định. Dùng khi máy không có màn hình vật lý (headless).
    /// </summary>
    private void EnsureScreenResolution(int width, int height)
    {
        var currentWidth  = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width  ?? 0;
        var currentHeight = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 0;

        if (currentWidth == width && currentHeight == height)
        {
            _logger.LogInformation("Screen resolution is already {W}x{H}. No change needed.", width, height);
            return;
        }

        _logger.LogWarning("Screen resolution is {CW}x{CH}. Forcing to {W}x{H}...", currentWidth, currentHeight, width, height);

        var devMode = new DEVMODE
        {
            dmDeviceName  = new string(' ', 32),
            dmFormName    = new string(' ', 32),
            dmSize        = (short)Marshal.SizeOf(typeof(DEVMODE)),
            dmPelsWidth   = width,
            dmPelsHeight  = height,
            dmFields      = 0x00080000 | 0x00100000 // DM_PELSWIDTH | DM_PELSHEIGHT
        };

        var result = ChangeDisplaySettings(ref devMode, CDS_UPDATEREGISTRY);
        if (result == DISP_CHANGE_SUCCESSFUL)
            _logger.LogInformation("Screen resolution forced to {W}x{H} successfully.", width, height);
        else
            _logger.LogWarning("ChangeDisplaySettings returned {Result}. Resolution may not have changed.", result);
    }

    private static (int R, int G, int B) GetPixelColor(int x, int y)
    {
        var hdc = GetDC(IntPtr.Zero);
        var color = GetPixel(hdc, x, y);
        ReleaseDC(IntPtr.Zero, hdc);
        return ((int)(color & 0xFF), (int)((color >> 8) & 0xFF), (int)((color >> 16) & 0xFF));
    }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    public GmesBrowserAutomation(AppSettings settings, ILogger<GmesBrowserAutomation> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> ExportAsync(
        DateTime startedAt,
        DownloadFileSelector downloadFileSelector,
        CancellationToken cancellationToken)
    {
        var driverDirectory = GmesBrowserDriverFactory.ResolveDriverDirectory(_settings);
        var options = GmesBrowserDriverFactory.CreateOptions(_settings);

        using var driver = new InternetExplorerDriver(
            driverDirectory,
            options,
            GmesBrowserDriverFactory.GetCommandTimeout(_settings));

        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(_settings.Timeouts.PageLoadWaitSeconds);

        await NavigateAndExportAsync(driver, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

        try
        {
            return downloadFileSelector.FindNewestDownload(startedAt);
        }
        catch (FileNotFoundException) when (_settings.Browser.UseDownloadSaveFallback)
        {
            _logger.LogInformation("Download file was not visible after Excel click. Sending Alt+S as Save fallback.");
            SendKeys.SendWait("%s");
            return await downloadFileSelector.WaitForNewestDownloadAsync(startedAt, cancellationToken);
        }
    }

    private async Task NavigateAndExportAsync(IWebDriver driver, CancellationToken cancellationToken)
    {
        // ── DPI Diagnostic ────────────────────────────────────────────────────────
        var screenBounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
        using var gfx = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        var dpiX = gfx.DpiX;
        var dpiY = gfx.DpiY;
        var scaleX = dpiX / 96.0;
        var scaleY = dpiY / 96.0;
        _logger.LogInformation(
            "Screen: Bounds={W}x{H} logical | DPI={DpiX}x{DpiY} | Scale={ScaleX:F2}x{ScaleY:F2} | Physical={PhW}x{PhH}",
            screenBounds?.Width, screenBounds?.Height,
            dpiX, dpiY,
            scaleX, scaleY,
            (int)((screenBounds?.Width ?? 0) * scaleX),
            (int)((screenBounds?.Height ?? 0) * scaleY));

        if (Math.Abs(scaleX - 1.0) > 0.01)
        {
            _logger.LogWarning(
                "DPI SCALE DETECTED: {Scale:P0}. Coordinates in appsettings.json must use LOGICAL pixels.",
                scaleX);
        }

        EnsureScreenResolution(1920, 1080);

        _logger.LogInformation("Browser opened to GMES URL {Url}. Waiting for login page.", _settings.GmesUrl);

        var loginSettleSeconds = Math.Max(3, _settings.Browser.LoginScreenSettleSeconds);
        await Task.Delay(TimeSpan.FromSeconds(loginSettleSeconds), cancellationToken);

        await LoginAsync(driver, cancellationToken);

        _logger.LogInformation("Waiting 5s for GMES main screen to render...");
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        // ── Step 1: Proc. Quality X (750, 185) ───────────────────────────────────
        ClickByCoordinates(_settings.Coordinates.ProcQualityX, "Proc. Quality X menu", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(2500), cancellationToken);

        // ── Step 2: Inspection History (750, 267) ────────────────────────────────
        SetCursorPos(_settings.Coordinates.InspectionHistory.X, _settings.Coordinates.InspectionHistory.Y);
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        ClickByCoordinates(_settings.Coordinates.InspectionHistory, "Inspection History submenu", driver);
        _logger.LogInformation("Waiting {Seconds}s for Inspection History screen to render...", _settings.Timeouts.SetScreenWaitSeconds);
        await Task.Delay(TimeSpan.FromSeconds(_settings.Timeouts.SetScreenWaitSeconds), cancellationToken);

        driver.SwitchTo().DefaultContent();

        // ── Step 3-5: Production Line dropdown → All → HS ────────────────────────
        _logger.LogInformation("Selecting Production Line: ALL then HS.");
        ClickByCoordinates(_settings.Coordinates.ProductionLine, "Production Line dropdown open", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownOpenDelayMs), cancellationToken);

        ClickByCoordinates(_settings.Coordinates.ProdLineAll, "Production Line ALL checkbox", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownClickDelayMs), cancellationToken);

        ClickByCoordinates(_settings.Coordinates.ProdLineHS, "Production Line HS checkbox", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownClickDelayMs), cancellationToken);

        // ── Step 6: Tab đóng Production Line dropdown ────────────────────────────
        _logger.LogInformation("Pressing TAB to close Production Line dropdown.");
        SendKeys.SendWait("{TAB}");
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownOpenDelayMs), cancellationToken);

        // ── Step 7-9: Process dropdown → All → AOI Inspection ───────────────────
        _logger.LogInformation("Selecting Process: ALL then AOI Inspection.");
        ClickByCoordinates(_settings.Coordinates.Process, "Process dropdown open", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownOpenDelayMs), cancellationToken);

        ClickByCoordinates(_settings.Coordinates.ProcessAll, "Process ALL checkbox", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownClickDelayMs), cancellationToken);

        ClickByCoordinates(_settings.Coordinates.AoiInspection, "Process AOI Inspection checkbox", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownClickDelayMs), cancellationToken);

        // ── Step 10: Tab đóng Process dropdown ───────────────────────────────────
        _logger.LogInformation("Pressing TAB to close Process dropdown.");
        SendKeys.SendWait("{TAB}");
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownOpenDelayMs), cancellationToken);

        // ── Step 11-12: Result filter → OK ───────────────────────────────────────
        _logger.LogInformation("Selecting Result filter: OK.");
        ClickByCoordinates(_settings.Coordinates.ResultFilter, "Result filter dropdown open", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownOpenDelayMs), cancellationToken);

        ClickByCoordinates(_settings.Coordinates.ResultOk, "Result OK option", driver);
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownClickDelayMs), cancellationToken);

        // ── Step 13: Tab đóng Result dropdown ────────────────────────────────────
        _logger.LogInformation("Pressing TAB to close Result dropdown.");
        SendKeys.SendWait("{TAB}");
        await Task.Delay(TimeSpan.FromMilliseconds(_settings.Timeouts.DropdownOpenDelayMs), cancellationToken);

        // ── Step 14: Search ───────────────────────────────────────────────────────
        ClickByCoordinates(_settings.Coordinates.Search, "Search button", driver);
        _logger.LogInformation("Waiting 10 seconds for search results to load...");
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        // ── Step 15: Excel export ─────────────────────────────────────────────────
        ClickByCoordinates(_settings.Coordinates.Excel, "Excel button", driver);
    }

    // Thử click theo text label trước; nếu không thấy thì thử SelectDropdownAfterLabel (cho dropdown)
    private static void ClickCheckboxOrDropdown(IWebDriver driver, string label, string optionText)
    {
        // Thử click trực tiếp theo text (checkbox / radio / button có text)
        var byText = FindByText(driver, optionText, exact: true)
                     ?? FindByText(driver, optionText);
        if (byText is not null)
        {
            ClickElement(driver, byText);
            return;
        }

        // Fallback: thử dạng dropdown sau label
        SelectDropdownAfterLabel(driver, label, optionText);
    }

    private async Task LoginAsync(IWebDriver driver, CancellationToken cancellationToken)
    {
        if (_settings.Browser.UseKeyboardLoginFallback)
        {
            _logger.LogInformation("Keyboard-first GMES login starting.");

            await GmesLoginKeyboardFallback.ExecuteAsync(
                _settings.GmesCredential.UserId,
                _settings.GmesCredential.Password,
                _logger,
                driver,
                cancellationToken);

            // Chờ trang main GMES load sau khi login
            var postLoginWait = Math.Max(3, _settings.Browser.LoginScreenSettleSeconds);
            _logger.LogInformation("Waiting {Seconds}s for GMES main page after login.", postLoginWait);
            await Task.Delay(TimeSpan.FromSeconds(postLoginWait), cancellationToken);

            return;
        }

        _logger.LogInformation("Waiting for GMES login fields.");
        var loginDetected = await WaitForAsync(
            () => FindFirstDisplayed(driver, By.CssSelector("input[type='password']")) is not null,
            TimeSpan.FromSeconds(Math.Min(10, _settings.Timeouts.LoginWaitSeconds)),
            "GMES login password field",
            cancellationToken,
            throwOnTimeout: false);

        if (!loginDetected)
        {
            throw new TimeoutException("Timed out waiting for GMES login password field.");
        }

        try
        {
            _logger.LogInformation("Selecting English on GMES login screen.");
            SelectFirstOptionByText(driver, "English");

            _logger.LogInformation("Entering GMES credentials.");
            SetLoginField(driver, FieldKind.UserId, SecretProtector.UnprotectConfiguredValue(_settings.GmesCredential.UserId));
            SetLoginField(driver, FieldKind.Password, SecretProtector.UnprotectConfiguredValue(_settings.GmesCredential.Password));

            _logger.LogInformation("Clicking GMES Login.");
            ClickLoginButton(driver);
        }
        catch (WebDriverException)
        {
            throw;
        }
    }

    private static void SelectFirstOptionByText(IWebDriver driver, string optionText)
    {
        foreach (var select in driver.FindElements(By.TagName("select")).Where(element => element.Displayed))
        {
            if (TrySelectOption(select, optionText))
            {
                return;
            }
        }

        throw new NoSuchElementException($"Could not find a select option with text '{optionText}'.");
    }

    private static void SelectDropdownAfterLabel(IWebDriver driver, string label, string optionText)
    {
        var select = FindFirstDisplayed(
            driver,
            By.XPath($"//*[normalize-space(.)='{label}']/following::select[1]"));

        if (select is not null && TrySelectOption(select, optionText))
        {
            return;
        }

        var input = FindFirstDisplayed(
            driver,
            By.XPath($"//*[normalize-space(.)='{label}']/following::input[1]"));

        if (input is not null)
        {
            ClickElement(driver, input);
            var option = FindByText(driver, optionText, exact: true) ?? FindByText(driver, optionText);
            if (option is not null)
            {
                ClickElement(driver, option);
                return;
            }
        }

        throw new NoSuchElementException($"Could not select '{optionText}' for '{label}'.");
    }

    private static bool TrySelectOption(IWebElement select, string optionText)
    {
        foreach (var option in select.FindElements(By.TagName("option")))
        {
            if (!string.Equals(option.Text.Trim(), optionText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            option.Click();
            return true;
        }

        return false;
    }

    private static void SetLoginField(IWebDriver driver, FieldKind fieldKind, string value)
    {
        var element = fieldKind == FieldKind.Password
            ? FindFirstDisplayed(driver, By.XPath("//input[@type='password']"))
            : FindFirstDisplayed(
                driver,
                By.XPath("//input[not(@type='hidden') and (@type='text' or not(@type)) and (contains(translate(@id, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'id') or contains(translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'id'))]"))
              ?? driver.FindElements(By.XPath("//input[not(@type='hidden') and (@type='text' or not(@type))]")).FirstOrDefault(input => input.Displayed);

        if (element is null)
        {
            throw new NoSuchElementException($"Could not find GMES {fieldKind} input.");
        }

        element.Clear();
        element.SendKeys(value);
    }

    private static void ClickByText(IWebDriver driver, string text, bool exact = false)
    {
        var element = FindByText(driver, text, exact)
            ?? throw new NoSuchElementException($"Could not find clickable text '{text}'.");

        ClickElement(driver, element);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    // Lấy top-level window handle của Edge — dùng MainWindowHandle (v10 approach)
    // KHÔNG dùng driver.CurrentWindowHandle vì trong IE Compatibility Mode nó trả về
    // handle của child window (IE rendering engine), không phải top-level Edge window.
    // Gọi SetForegroundWindow trên child window có thể làm Edge dịch chuyển layout.
    private static IntPtr ResolveEdgeWindowHandle(IWebDriver? driver)
    {
        // Tìm cửa sổ msedge có title chứa "GMES" — ưu tiên đúng trang trước
        var processes = Process.GetProcessesByName("msedge");
        foreach (var p in processes.Where(p => p.MainWindowHandle != IntPtr.Zero))
        {
            var sb = new System.Text.StringBuilder(512);
            GetWindowText(p.MainWindowHandle, sb, sb.Capacity);
            if (sb.ToString().Contains("GMES", StringComparison.OrdinalIgnoreCase))
                return p.MainWindowHandle;
        }

        // Fallback: lấy bất kỳ msedge process có MainWindowHandle
        return processes.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero)?.MainWindowHandle ?? IntPtr.Zero;
    }

    private void ClickByCoordinates(PointOptions point, string description, IWebDriver? driver = null)
    {
        _logger.LogInformation("Clicking {Description} by absolute screen coordinates (X: {X}, Y: {Y}).", description, point.X, point.Y);

        var targetHandle = ResolveEdgeWindowHandle(driver);
        if (targetHandle != IntPtr.Zero)
        {
            // Chỉ gọi SW_RESTORE khi window đang bị minimize
            // Nếu đang maximize mà gọi SW_RESTORE sẽ khiến window thu nhỏ lại → menu dịch vị trí → click lệch!
            if (IsIconic(targetHandle))
            {
                _logger.LogInformation("Window is minimized. Restoring...");
                ShowWindow(targetHandle, SW_RESTORE);
                Thread.Sleep(300);
            }
            else
            {
                // Window đang hiển thị bình thường hoặc maximize: chỉ bring to front, giữ nguyên size
                ShowWindow(targetHandle, SW_SHOW);
                Thread.Sleep(50);
            }
            SetForegroundWindow(targetHandle);
            Thread.Sleep(250); // Tăng từ 150ms → 250ms cho IE Mode bridge
        }

        SetCursorPos(point.X, point.Y);
        Thread.Sleep(600); // Tăng từ 300ms → 600ms: IE Mode iframe cần thời gian nhận hover state
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(150); // Tăng từ 100ms → 150ms
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
    }



    private static void ClickLoginButton(IWebDriver driver)
    {
        var loginButton = FindFirstDisplayed(
                driver,
                By.XPath("//button[normalize-space(.)='Login'] | //input[(@type='button' or @type='submit') and normalize-space(@value)='Login']"))
            ?? FindByText(driver, "Login", exact: true)
            ?? throw new NoSuchElementException("Could not find GMES Login button.");

        ClickElement(driver, loginButton);
    }

    private static IWebElement? FindByText(IWebDriver driver, string text, bool exact = false)
    {
        driver.SwitchTo().DefaultContent();
        return SearchFrameTreeForText(driver, text, exact);
    }

    private static IWebElement? SearchFrameTreeForText(IWebDriver driver, string text, bool exact)
    {
        var escaped = ToXPathLiteral(text);
        var condition = exact
            ? $"normalize-space(.)={escaped}"
            : $"contains(normalize-space(.), {escaped})";

        var element = driver.FindElements(By.XPath($"//*[not(self::script) and not(self::style) and {condition}]"))
            .Where(e => e.Displayed)
            .OrderBy(e => e.Text.Trim().Length)
            .FirstOrDefault();

        if (element != null) return element;

        var frames = driver.FindElements(By.TagName("frame")).Concat(driver.FindElements(By.TagName("iframe"))).ToList();
        for (int i = 0; i < frames.Count; i++)
        {
            try
            {
                driver.SwitchTo().Frame(i);
                var found = SearchFrameTreeForText(driver, text, exact);
                if (found != null) return found;
                driver.SwitchTo().ParentFrame();
            }
            catch (WebDriverException)
            {
                driver.SwitchTo().ParentFrame();
            }
        }
        return null;
    }

    private static IWebElement? FindFirstDisplayed(IWebDriver driver, By by)
    {
        driver.SwitchTo().DefaultContent();
        return SearchFrameTreeForElement(driver, by);
    }

    private static IWebElement? SearchFrameTreeForElement(IWebDriver driver, By by)
    {
        var element = driver.FindElements(by).FirstOrDefault(e => e.Displayed);
        if (element != null) return element;

        var frames = driver.FindElements(By.TagName("frame")).Concat(driver.FindElements(By.TagName("iframe"))).ToList();
        for (int i = 0; i < frames.Count; i++)
        {
            try
            {
                driver.SwitchTo().Frame(i);
                var found = SearchFrameTreeForElement(driver, by);
                if (found != null) return found;
                driver.SwitchTo().ParentFrame();
            }
            catch (WebDriverException)
            {
                driver.SwitchTo().ParentFrame();
            }
        }
        return null;
    }

    private static void ClickElement(IWebDriver driver, IWebElement element)
    {
        try
        {
            element.Click();
        }
        catch (WebDriverException)
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true); arguments[0].click();", element);
        }
    }

    private static async Task<bool> WaitForAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string description,
        CancellationToken cancellationToken,
        bool throwOnTimeout = true)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        if (throwOnTimeout)
        {
            throw new TimeoutException($"Timed out waiting for {description}.");
        }

        return false;
    }

    private static string ToXPathLiteral(string value)
    {
        if (!value.Contains('\''))
        {
            return $"'{value}'";
        }

        if (!value.Contains('"'))
        {
            return $"\"{value}\"";
        }

        return "concat('" + value.Replace("'", "', \"'\", '", StringComparison.Ordinal) + "')";
    }

    private enum FieldKind
    {
        UserId,
        Password
    }
}

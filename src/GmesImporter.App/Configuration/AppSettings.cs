namespace GmesImporter.App.Configuration;

public sealed class AppSettings
{
    public string GmesUrl { get; set; } = "http://10.224.5.14/";

    public int RunIntervalSeconds { get; set; } = 900;

    public string DownloadDirectory { get; set; } = "%USERPROFILE%\\Downloads";

    public string ArchiveDirectory { get; set; } = "%LOCALAPPDATA%\\GmesImporter\\archive";

    public string FailedDirectory { get; set; } = "%LOCALAPPDATA%\\GmesImporter\\failed";

    public string DownloadPattern { get; set; } = "Excel_Export_*.xlsx";

    public TimeoutsOptions Timeouts { get; set; } = new();

    public BrowserOptions Browser { get; set; } = new();

    public DatabaseOptions Database { get; set; } = new();

    public GmesCredentialOptions GmesCredential { get; set; } = new();

    public CoordinateOptions Coordinates { get; set; } = new();

    public string ResolvedDownloadDirectory => Environment.ExpandEnvironmentVariables(DownloadDirectory);

    public string ResolvedArchiveDirectory => Environment.ExpandEnvironmentVariables(ArchiveDirectory);

    public string ResolvedFailedDirectory => Environment.ExpandEnvironmentVariables(FailedDirectory);

    public SlmsOptions Slms { get; set; } = new();
}

public sealed class TimeoutsOptions
{
    public int LoginWaitSeconds { get; set; } = 30;

    public int PageLoadWaitSeconds { get; set; } = 45;

    public int SearchWaitSeconds { get; set; } = 25;

    public int SetScreenWaitSeconds { get; set; } = 15;

    public int DownloadWaitSeconds { get; set; } = 60;

    public int RetryCount { get; set; } = 2;

    /// <summary>Thời gian chờ (ms) sau khi click MỞ dropdown — đủ lâu để animation dropdown IE Mode hoàn thành.</summary>
    public int DropdownOpenDelayMs { get; set; } = 2500;

    /// <summary>Thời gian chờ (ms) giữa mỗi lần click vào item trong dropdown (ALL, HS, PCB Input...).</summary>
    public int DropdownClickDelayMs { get; set; } = 1200;
}

public sealed class BrowserOptions
{
    public string EdgeExecutablePath { get; set; } = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";

    public string IeDriverPath { get; set; } = "IEDriverServer.exe";

    public int DriverCommandTimeoutSeconds { get; set; } = 180;

    public bool UseKeyboardLoginFallback { get; set; } = true;

    public int LoginScreenSettleSeconds { get; set; } = 3;

    public bool UseDownloadSaveFallback { get; set; } = true;
}

public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
}

public sealed class GmesCredentialOptions
{
    public string UserId { get; set; } = "";

    public string Password { get; set; } = "";
}

public sealed class CoordinateOptions
{
    // ── Step 1: Menu chính ──────────────────────────────────────────────────────
    public PointOptions ProcQualityX { get; set; } = new() { X = 750, Y = 185 };

    // ── Step 2: Sub-menu ────────────────────────────────────────────────────────
    public PointOptions InspectionHistory { get; set; } = new() { X = 750, Y = 267 };

    // ── Step 3-5: Production Line dropdown ──────────────────────────────────────
    public PointOptions ProductionLine { get; set; } = new() { X = 180, Y = 463 };
    public PointOptions ProdLineAll { get; set; } = new() { X = 180, Y = 492 };
    public PointOptions ProdLineHS { get; set; } = new() { X = 180, Y = 815 };

    // ── Step 7-9: Process dropdown ───────────────────────────────────────────────
    public PointOptions Process { get; set; } = new() { X = 278, Y = 494 };
    public PointOptions ProcessAll { get; set; } = new() { X = 278, Y = 517 };
    public PointOptions AoiInspection { get; set; } = new() { X = 278, Y = 707 };

    // ── Step 11-12: Result filter ────────────────────────────────────────────────
    public PointOptions ResultFilter { get; set; } = new() { X = 278, Y = 558 };
    public PointOptions ResultOk { get; set; } = new() { X = 278, Y = 605 };

    // ── Step 14: Search ──────────────────────────────────────────────────────────
    public PointOptions Search { get; set; } = new() { X = 247, Y = 728 };

    // ── Step 15: Excel export ────────────────────────────────────────────────────
    public PointOptions Excel { get; set; } = new() { X = 1862, Y = 288 };
}

public sealed class PointOptions
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class SlmsOptions
{
    public bool Enabled { get; set; } = true;
    public string ExecutablePath { get; set; } = @"C:\SmartLMS_PC 2.0.2.7\SmartLMS.PC\SmartLMS.exe";
    public int RunIntervalSeconds { get; set; } = 3600;
    public string UserId { get; set; } = "2023020087";
    public string Password { get; set; } = "1";
    public SlmsCoordinateOptions Coordinates { get; set; } = new();
}

public sealed class SlmsCoordinateOptions
{
    public PointOptions SmtProductionManage { get; set; } = new() { X = 647, Y = 39 };
    public PointOptions Stencil { get; set; } = new() { X = 99, Y = 99 };
    public PointOptions StencilManagement { get; set; } = new() { X = 150, Y = 140 };
    public PointOptions StencilInformationTable { get; set; } = new() { X = 400, Y = 310 };
    public PointOptions ExportToExcel { get; set; } = new() { X = 460, Y = 316 };
}

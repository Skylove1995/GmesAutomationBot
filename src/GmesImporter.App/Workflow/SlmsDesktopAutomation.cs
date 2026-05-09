using System.Diagnostics;
using GmesImporter.App.Configuration;
using Microsoft.Extensions.Logging;

namespace GmesImporter.App.Workflow;

public sealed class SlmsDesktopAutomation
{
    private readonly AppSettings _settings;
    private readonly ILogger<SlmsDesktopAutomation> _logger;

    public SlmsDesktopAutomation(AppSettings settings, ILogger<SlmsDesktopAutomation> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public string ExportAsync(DateTime startedAt)
    {
        var slmsConfig = _settings.Slms;
        if (string.IsNullOrEmpty(slmsConfig.ExecutablePath) || !File.Exists(slmsConfig.ExecutablePath))
        {
            throw new FileNotFoundException($"SLMS executable not found at {slmsConfig.ExecutablePath}");
        }

        var processName = Path.GetFileNameWithoutExtension(slmsConfig.ExecutablePath);
        var existingProcesses = Process.GetProcessesByName(processName);
        Process process;

        if (existingProcesses.Length == 0)
        {
            _logger.LogInformation("Starting SLMS Application...");
            // UseShellExecute=true: cho phép Windows xử lý UAC elevation (Error 740 fix)
            // Nếu GmesImporter chạy với quyền Admin, process con sẽ được kế thừa elevation
            var startInfo = new ProcessStartInfo(slmsConfig.ExecutablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(slmsConfig.ExecutablePath) ?? ""
            };
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start SLMS process.");
            Thread.Sleep(5000); // Wait for application to load
        }
        else
        {
            _logger.LogInformation("SLMS Application is already running.");
            process = existingProcesses.First();
        }

        Win32Input.FocusWindow(processName);
        Thread.Sleep(1000);

        _logger.LogInformation("Entering Login Information...");
        Win32Input.SendKeysWait(slmsConfig.UserId, 500);
        Win32Input.SendKeysWait("{TAB}", 500);
        Win32Input.SendKeysWait(slmsConfig.Password, 500);
        Win32Input.SendKeysWait("{TAB}", 500);
        Win32Input.SendKeysWait("{DOWN}", 500);
        Win32Input.SendKeysWait("{ENTER}", 3000); // Wait for login to complete

        _logger.LogInformation("Clicking SMT Production Manage...");
        Win32Input.ClickLeft(slmsConfig.Coordinates.SmtProductionManage, 1000);

        _logger.LogInformation("Clicking Stencil...");
        Win32Input.ClickLeft(slmsConfig.Coordinates.Stencil, 1000);

        _logger.LogInformation("Clicking Stencil Management...");
        Win32Input.ClickLeft(slmsConfig.Coordinates.StencilManagement, 3000); // Wait for screen to load

        _logger.LogInformation("Right clicking Stencil Information Table...");
        Win32Input.ClickRight(slmsConfig.Coordinates.StencilInformationTable, 1000);

        _logger.LogInformation("Clicking Export to Excel...");
        Win32Input.ClickLeft(slmsConfig.Coordinates.ExportToExcel, 3000); // Wait for Save dialog

        var fileName = $"StencilExport_{startedAt:yyyyMMdd_HHmmss}.xlsx";
        var exportPath = Path.Combine(_settings.ResolvedDownloadDirectory, fileName);
        
        _logger.LogInformation("Saving Excel file as: {FileName}", fileName);
        Win32Input.SendKeysWait(exportPath, 1000);
        Win32Input.SendKeysWait("{ENTER}", 5000); // Wait for save to complete

        if (!File.Exists(exportPath))
        {
            throw new FileNotFoundException("Exported Excel file was not found after saving.", exportPath);
        }

        return exportPath;
    }
}

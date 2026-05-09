using GmesImporter.App.Configuration;
using GmesImporter.App.Database;
using GmesImporter.App.Files;
using GmesImporter.App.Security;
using GmesImporter.Core.Excel;
using GmesImporter.Core.Importing;
using Microsoft.Extensions.Logging;

namespace GmesImporter.App.Workflow;

public sealed class ImportRunner
{
    private readonly AppSettings _settings;
    private readonly ILogger<ImportRunner> _logger;
    private readonly GmesBrowserAutomation _browserAutomation;
    private readonly DownloadFileSelector _downloadFileSelector;
    private readonly FileArchiver _fileArchiver;
    private readonly Func<string, Task<MySqlProductionRepository>> _repositoryFactory;

    public ImportRunner(
        AppSettings settings,
        ILogger<ImportRunner> logger,
        GmesBrowserAutomation browserAutomation,
        DownloadFileSelector downloadFileSelector,
        FileArchiver fileArchiver,
        Func<string, Task<MySqlProductionRepository>> repositoryFactory)
    {
        _settings = settings;
        _logger = logger;
        _browserAutomation = browserAutomation;
        _downloadFileSelector = downloadFileSelector;
        _fileArchiver = fileArchiver;
        _repositoryFactory = repositoryFactory;
    }

    public async Task<ImportRunResult> RunOnceAsync(bool dryRun, string? sourceFile, CancellationToken cancellationToken)
    {
        var downloadedFile = sourceFile ?? await ExportWithRetryAsync(cancellationToken);
        try
        {
            var records = ProductionExcelParser.Parse(downloadedFile);
            _logger.LogInformation("Parsed {Count} production rows from {File}.", records.Count, downloadedFile);

            var connectionString = SecretProtector.UnprotectConfiguredValue(_settings.Database.ConnectionString);
            await using var repository = await _repositoryFactory(connectionString);
            var service = new ProductionImportService(repository);
            var summary = await service.ImportAsync(records, dryRun, cancellationToken);

            if (dryRun)
            {
                await repository.RollbackAsync(cancellationToken);
                _logger.LogInformation("Dry-run completed. Database changes were not committed.");
            }
            else
            {
                await repository.CommitAsync(cancellationToken);
            }

            if (sourceFile is null)
            {
                downloadedFile = _fileArchiver.ArchiveSuccess(downloadedFile);
            }

            return new ImportRunResult(downloadedFile, summary);
        }
        catch
        {
            if (sourceFile is null && File.Exists(downloadedFile))
            {
                _fileArchiver.ArchiveFailure(downloadedFile);
            }

            throw;
        }
    }

    private async Task<string> ExportWithRetryAsync(CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var attempts = Math.Max(1, _settings.Timeouts.RetryCount + 1);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var startedAt = DateTime.Now;
                _logger.LogInformation("Starting GMES export attempt {Attempt}/{Attempts}.", attempt, attempts);
                return await _browserAutomation.ExportAsync(startedAt, _downloadFileSelector, cancellationToken);
            }
            catch (Exception exception)
            {
                lastException = exception;
                _logger.LogWarning(exception, "GMES export attempt {Attempt}/{Attempts} failed.", attempt, attempts);
            }
        }

        throw new InvalidOperationException("GMES export failed after retry attempts.", lastException);
    }
}

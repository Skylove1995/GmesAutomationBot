using GmesImporter.App.Configuration;
using GmesImporter.App.Database;
using GmesImporter.Core.Excel;
using GmesImporter.App.Security;
using Microsoft.Extensions.Logging;

namespace GmesImporter.App.Workflow;

public sealed class SlmsImportRunner
{
    private readonly AppSettings _settings;
    private readonly ILogger<SlmsImportRunner> _logger;
    private readonly SlmsDesktopAutomation _slmsAutomation;
    private readonly Func<string, Task<MySqlStencilRepository>> _repositoryFactory;

    public SlmsImportRunner(
        AppSettings settings,
        ILogger<SlmsImportRunner> logger,
        SlmsDesktopAutomation slmsAutomation,
        Func<string, Task<MySqlStencilRepository>> repositoryFactory)
    {
        _settings = settings;
        _logger = logger;
        _slmsAutomation = slmsAutomation;
        _repositoryFactory = repositoryFactory;
    }

    public async Task RunOnceAsync(bool dryRun, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var attempts = Math.Max(1, _settings.Timeouts.RetryCount + 1);
        string? downloadedFile = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var startedAt = DateTime.Now;
                _logger.LogInformation("Starting SLMS export attempt {Attempt}/{Attempts}.", attempt, attempts);
                
                // 1. Export Excel via UI Automation
                downloadedFile = _slmsAutomation.ExportAsync(startedAt);
                
                // 2. Parse Excel
                var records = StencilExcelParser.Parse(downloadedFile);
                _logger.LogInformation("Parsed {Count} stencil records from {File}.", records.Count, downloadedFile);

                // 3. Update Database
                var connectionString = SecretProtector.UnprotectConfiguredValue(_settings.Database.ConnectionString);
                await using var repository = await _repositoryFactory(connectionString);
                
                var affectedRows = await repository.UpsertBatchAsync(records, cancellationToken);
                _logger.LogInformation("SLMS Import affected {Count} rows.", affectedRows);

                if (dryRun)
                {
                    await repository.RollbackAsync(cancellationToken);
                    _logger.LogInformation("Dry-run completed. SLMS Database changes were not committed.");
                }
                else
                {
                    await repository.CommitAsync(cancellationToken);
                }

                // If successful, break retry loop
                break;
            }
            catch (Exception exception)
            {
                lastException = exception;
                _logger.LogWarning(exception, "SLMS export/import attempt {Attempt}/{Attempts} failed.", attempt, attempts);
                
                if (attempt == attempts)
                {
                    throw new InvalidOperationException("SLMS export/import failed after retry attempts.", lastException);
                }
            }
        }
    }
}

using GmesImporter.App.Configuration;
using Microsoft.Extensions.Logging;

namespace GmesImporter.App.Workflow;

public sealed class ApplicationLoopCoordinator
{
    private readonly AppSettings _settings;
    private readonly ILogger<ApplicationLoopCoordinator> _logger;
    private readonly ImportRunner _gmesRunner;
    private readonly SlmsImportRunner _slmsRunner;
    private readonly SemaphoreSlim _automationLock = new(1, 1);

    public ApplicationLoopCoordinator(
        AppSettings settings,
        ILogger<ApplicationLoopCoordinator> logger,
        ImportRunner gmesRunner,
        SlmsImportRunner slmsRunner)
    {
        _settings = settings;
        _logger = logger;
        _gmesRunner = gmesRunner;
        _slmsRunner = slmsRunner;
    }

    public async Task RunLoopsAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var gmesTask = RunGmesLoopAsync(dryRun, cancellationToken);
        var slmsTask = Task.CompletedTask;

        if (_settings.Slms.Enabled)
        {
            slmsTask = RunSlmsLoopAsync(dryRun, cancellationToken);
        }
        else
        {
            _logger.LogInformation("SLMS Import is disabled in configuration.");
        }

        await Task.WhenAll(gmesTask, slmsTask);
    }

    private async Task RunGmesLoopAsync(bool dryRun, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _automationLock.WaitAsync(cancellationToken);
            try
            {
                var result = await _gmesRunner.RunOnceAsync(dryRun, null, cancellationToken);
                _logger.LogInformation(
                    "GMES Import result: inserted={Inserted}, updated={Updated}, skipped={Skipped}, file={File}",
                    result.Summary.Inserted,
                    result.Summary.Updated,
                    result.Summary.Skipped,
                    result.SourceFile);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during GMES cycle. Continuing to next loop.");
            }
            finally
            {
                _automationLock.Release();
            }

            if (cancellationToken.IsCancellationRequested) break;

            _logger.LogInformation("GMES Loop: Waiting {Seconds} seconds for next run.", _settings.RunIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.RunIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException) { }
        }
    }

    private async Task RunSlmsLoopAsync(bool dryRun, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _automationLock.WaitAsync(cancellationToken);
            try
            {
                await _slmsRunner.RunOnceAsync(dryRun, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during SLMS cycle. Continuing to next loop.");
            }
            finally
            {
                _automationLock.Release();
            }

            if (cancellationToken.IsCancellationRequested) break;

            _logger.LogInformation("SLMS Loop: Waiting {Seconds} seconds for next run.", _settings.Slms.RunIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.Slms.RunIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException) { }
        }
    }
}

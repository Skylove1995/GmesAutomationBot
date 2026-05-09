using GmesImporter.App.Configuration;
using GmesImporter.App.Database;
using GmesImporter.App.Files;
using GmesImporter.App.Security;
using GmesImporter.App.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GmesImporter.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            });
        });

        var logger = loggerFactory.CreateLogger("GmesImporter");

        try
        {
            var command = CommandLine.Parse(args);
            if (command.Name is "help" or "--help" or "-h")
            {
                Console.WriteLine(CommandLine.HelpText);
                return 0;
            }

            if (command.Name == "protect-text")
            {
                var plainText = command.GetRequiredValue("value");
                Console.WriteLine(SecretProtector.Protect(plainText));
                return 0;
            }

            var settings = LoadSettings();
            var validator = new ConfigurationValidator(settings, loggerFactory.CreateLogger<ConfigurationValidator>());

            if (command.Name == "validate-config")
            {
                var validation = await validator.ValidateAsync(CancellationToken.None);
                Console.WriteLine(validation.ToDisplayText());
                return validation.IsValid ? 0 : 2;
            }

            if (command.Name == "diagnose-db")
            {
                return await DatabaseDiagnostics.RunAsync(settings.Database.ConnectionString, CancellationToken.None);
            }

            var dryRun = command.Name == "dry-run" || command.HasFlag("dry-run");
            if (command.Name is not ("run-once" or "dry-run" or "run-loop"))
            {
                Console.Error.WriteLine($"Unknown command '{command.Name}'.");
                Console.WriteLine(CommandLine.HelpText);
                return 2;
            }

            validator.ValidateForRun();

            var runner = new ImportRunner(
                settings,
                loggerFactory.CreateLogger<ImportRunner>(),
                new GmesBrowserAutomation(settings, loggerFactory.CreateLogger<GmesBrowserAutomation>()),
                new DownloadFileSelector(settings),
                new FileArchiver(settings, loggerFactory.CreateLogger<FileArchiver>()),
                connectionString => MySqlProductionRepository.OpenAsync(
                    connectionString,
                    loggerFactory.CreateLogger<MySqlProductionRepository>(),
                    CancellationToken.None));

            var slmsRunner = new SlmsImportRunner(
                settings,
                loggerFactory.CreateLogger<SlmsImportRunner>(),
                new SlmsDesktopAutomation(settings, loggerFactory.CreateLogger<SlmsDesktopAutomation>()),
                connectionString => MySqlStencilRepository.OpenAsync(
                    connectionString,
                    loggerFactory.CreateLogger<MySqlStencilRepository>(),
                    CancellationToken.None));

            var coordinator = new ApplicationLoopCoordinator(
                settings,
                loggerFactory.CreateLogger<ApplicationLoopCoordinator>(),
                runner,
                slmsRunner);

            if (command.Name == "run-loop")
            {
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    logger.LogWarning("Stopping loop...");
                    e.Cancel = true;
                    cts.Cancel();
                };

                await coordinator.RunLoopsAsync(dryRun, cts.Token);
                return 0;
            }
            else
            {
                var filePath = command.GetOptionalValue("file");
                if (filePath != null)
                {
                    // If a specific file is provided, only run GMES import with that file.
                    var result = await runner.RunOnceAsync(dryRun, filePath, CancellationToken.None);

                    logger.LogInformation(
                        "Import result: inserted={Inserted}, updated={Updated}, skipped={Skipped}, file={File}",
                        result.Summary.Inserted,
                        result.Summary.Updated,
                        result.Summary.Skipped,
                        result.SourceFile);
                }
                else
                {
                    // For run-once without a file, run both once sequentially (with dry-run support)
                    await runner.RunOnceAsync(dryRun, null, CancellationToken.None);
                    if (settings.Slms.Enabled)
                    {
                        await slmsRunner.RunOnceAsync(dryRun, CancellationToken.None);
                    }
                }

                return 0;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Importer failed.");
            return 1;
        }
    }

    private static AppSettings LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables("GMES_IMPORTER_")
            .Build();

        return configuration.Get<AppSettings>() ?? throw new InvalidOperationException("appsettings.json is empty.");
    }
}

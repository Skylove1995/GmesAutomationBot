using GmesImporter.App.Database;
using GmesImporter.App.Security;
using Microsoft.Extensions.Logging;

namespace GmesImporter.App.Configuration;

public sealed class ConfigurationValidator
{
    private readonly AppSettings _settings;
    private readonly ILogger<ConfigurationValidator> _logger;

    public ConfigurationValidator(AppSettings settings, ILogger<ConfigurationValidator> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void ValidateForRun()
    {
        var errors = ValidateCommon();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken)
    {
        var errors = ValidateCommon();
        var warnings = new List<string>();

        TryCreateDirectory(_settings.ResolvedDownloadDirectory, errors);
        TryCreateDirectory(_settings.ResolvedArchiveDirectory, errors);
        TryCreateDirectory(_settings.ResolvedFailedDirectory, errors);

        if (!File.Exists(ResolvePath(_settings.Browser.EdgeExecutablePath)))
        {
            warnings.Add($"Edge executable was not found: {_settings.Browser.EdgeExecutablePath}");
        }

        if (!File.Exists(ResolvePath(_settings.Browser.IeDriverPath)))
        {
            warnings.Add($"IEDriverServer.exe was not found: {_settings.Browser.IeDriverPath}");
        }

        if (errors.Count == 0)
        {
            try
            {
                var connectionString = SecretProtector.UnprotectConfiguredValue(_settings.Database.ConnectionString);
                await MySqlProductionRepository.TestConnectionAsync(connectionString, cancellationToken);
            }
            catch (Exception exception)
            {
                errors.Add($"Database validation failed: {exception.Message}");
            }
        }

        foreach (var warning in warnings)
        {
            _logger.LogWarning("{Warning}", warning);
        }

        return new ValidationResult(errors, warnings);
    }

    private List<string> ValidateCommon()
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(_settings.GmesUrl, UriKind.Absolute, out _))
        {
            errors.Add($"GmesUrl is not a valid absolute URL: {_settings.GmesUrl}");
        }

        if (_settings.RunIntervalSeconds <= 0)
        {
            errors.Add("RunIntervalSeconds must be greater than zero.");
        }

        ValidateSecret(_settings.Database.ConnectionString, "Database.ConnectionString", errors);
        ValidateSecret(_settings.GmesCredential.UserId, "GmesCredential.UserId", errors);
        ValidateSecret(_settings.GmesCredential.Password, "GmesCredential.Password", errors);

        return errors;
    }

    private static void ValidateSecret(string value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
            return;
        }

        if (!value.StartsWith(SecretProtector.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    }

    private static void TryCreateDirectory(string path, List<string> errors)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception exception)
        {
            errors.Add($"Cannot create or access directory '{path}': {exception.Message}");
        }
    }

    private static string ResolvePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(AppContext.BaseDirectory, expanded);
    }
}

public sealed record ValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;

    public string ToDisplayText()
    {
        var lines = new List<string> { IsValid ? "Configuration is valid." : "Configuration is invalid." };
        lines.AddRange(Errors.Select(error => $"ERROR: {error}"));
        lines.AddRange(Warnings.Select(warning => $"WARNING: {warning}"));
        return string.Join(Environment.NewLine, lines);
    }
}

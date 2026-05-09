using GmesImporter.App.Configuration;
using Microsoft.Extensions.Logging;

namespace GmesImporter.App.Files;

public sealed class FileArchiver
{
    private readonly AppSettings _settings;
    private readonly ILogger<FileArchiver> _logger;

    public FileArchiver(AppSettings settings, ILogger<FileArchiver> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public string ArchiveSuccess(string filePath)
    {
        var targetDirectory = Path.Combine(_settings.ResolvedArchiveDirectory, DateTime.Now.ToString("yyyyMMdd"));
        return MoveToDirectory(filePath, targetDirectory);
    }

    public string ArchiveFailure(string filePath)
    {
        var targetDirectory = Path.Combine(_settings.ResolvedFailedDirectory, DateTime.Now.ToString("yyyyMMdd"));
        return MoveToDirectory(filePath, targetDirectory);
    }

    private string MoveToDirectory(string filePath, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var targetPath = BuildAvailablePath(targetDirectory, Path.GetFileName(filePath));
        File.Move(filePath, targetPath);
        _logger.LogInformation("Moved {Source} to {Target}.", filePath, targetPath);
        return targetPath;
    }

    private static string BuildAvailablePath(string directory, string fileName)
    {
        var targetPath = Path.Combine(directory, fileName);
        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return Path.Combine(directory, $"{name}_{DateTime.Now:HHmmssfff}{extension}");
    }
}

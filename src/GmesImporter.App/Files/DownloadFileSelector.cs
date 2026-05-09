using GmesImporter.App.Configuration;

namespace GmesImporter.App.Files;

public sealed class DownloadFileSelector
{
    private readonly AppSettings _settings;

    public DownloadFileSelector(AppSettings settings)
    {
        _settings = settings;
    }

    public string FindNewestDownload(DateTime startedAt)
    {
        var directory = _settings.ResolvedDownloadDirectory;
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Download directory does not exist: {directory}");
        }

        var file = Directory.EnumerateFiles(directory, _settings.DownloadPattern)
            .Select(path => new FileInfo(path))
            .Where(info => info.CreationTime >= startedAt || info.LastWriteTime >= startedAt)
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .FirstOrDefault();

        return file?.FullName
            ?? throw new FileNotFoundException($"No GMES Excel download matching '{_settings.DownloadPattern}' was found in {directory}.");
    }

    public async Task<string> WaitForNewestDownloadAsync(DateTime startedAt, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_settings.Timeouts.DownloadWaitSeconds);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var file = FindNewestDownload(startedAt);
                if (!IsFileLocked(file))
                {
                    return file;
                }
            }
            catch (FileNotFoundException)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return FindNewestDownload(startedAt);
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}

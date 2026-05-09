using System.Text.Json;

namespace GmesImporter.Tests;

public sealed class AppSettingsFileTests
{
    [Fact]
    public void Appsettings_sets_set_screen_wait_to_15_seconds()
    {
        var appsettingsPath = FindRepositoryFile("src", "GmesImporter.App", "appsettings.example.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appsettingsPath));

        var waitSeconds = document.RootElement
            .GetProperty("Timeouts")
            .GetProperty("SetScreenWaitSeconds")
            .GetInt32();

        Assert.Equal(15, waitSeconds);
    }

    private static string FindRepositoryFile(params string[] relativePathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(relativePathParts));
    }
}

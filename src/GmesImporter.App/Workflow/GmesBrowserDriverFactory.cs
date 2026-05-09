using GmesImporter.App.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.IE;

namespace GmesImporter.App.Workflow;

public static class GmesBrowserDriverFactory
{
    public static InternetExplorerOptions CreateOptions(AppSettings settings)
    {
        return new InternetExplorerOptions
        {
            AttachToEdgeChrome = true,
            EdgeExecutablePath = ResolvePath(settings.Browser.EdgeExecutablePath),
            IgnoreZoomLevel = true,
            IntroduceInstabilityByIgnoringProtectedModeSettings = true,
            PageLoadStrategy = PageLoadStrategy.None,
            InitialBrowserUrl = settings.GmesUrl  // mở thẳng URL khi browser khởi tạo, bypass GoToUrl hang
        };
    }

    public static TimeSpan GetCommandTimeout(AppSettings settings)
    {
        var seconds = Math.Max(
            settings.Browser.DriverCommandTimeoutSeconds,
            settings.Timeouts.PageLoadWaitSeconds + settings.Timeouts.LoginWaitSeconds);

        return TimeSpan.FromSeconds(seconds);
    }

    public static string ResolveDriverDirectory(AppSettings settings)
    {
        var driverPath = ResolvePath(settings.Browser.IeDriverPath);
        return Path.GetDirectoryName(driverPath) ?? AppContext.BaseDirectory;
    }

    public static string ResolvePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(AppContext.BaseDirectory, expanded);
    }
}

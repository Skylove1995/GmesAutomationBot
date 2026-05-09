using GmesImporter.App.Configuration;
using GmesImporter.App.Workflow;
using OpenQA.Selenium;

namespace GmesImporter.Tests;

public sealed class GmesBrowserDriverFactoryTests
{
    [Fact]
    public void CreateOptions_uses_non_blocking_page_load_strategy_for_ie_mode()
    {
        var settings = new AppSettings();

        var options = GmesBrowserDriverFactory.CreateOptions(settings);

        Assert.Equal(PageLoadStrategy.None, options.PageLoadStrategy);
    }

    [Fact]
    public void GetCommandTimeout_is_longer_than_page_load_wait()
    {
        var settings = new AppSettings
        {
            Timeouts = new TimeoutsOptions { PageLoadWaitSeconds = 45 },
            Browser = new BrowserOptions { DriverCommandTimeoutSeconds = 180 }
        };

        var timeout = GmesBrowserDriverFactory.GetCommandTimeout(settings);

        Assert.Equal(TimeSpan.FromSeconds(180), timeout);
    }

    [Fact]
    public void EscapeForSendKeys_escapes_password_modifier_characters()
    {
        var escaped = GmesLoginKeyboardFallback.EscapeForSendKeys("a+b^c%d~e");

        Assert.Equal("a{+}b{^}c{%}d{~}e", escaped);
    }

    [Fact]
    public void BrowserOptions_defaults_to_keyboard_first_login()
    {
        var options = new BrowserOptions();

        Assert.True(options.UseKeyboardLoginFallback);
        Assert.Equal(3, options.LoginScreenSettleSeconds);
    }
}

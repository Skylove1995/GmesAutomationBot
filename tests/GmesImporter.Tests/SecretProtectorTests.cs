using GmesImporter.App.Security;

namespace GmesImporter.Tests;

public sealed class SecretProtectorTests
{
    [Fact]
    public void UnprotectConfiguredValue_returns_plaintext_values_unchanged()
    {
        const string connectionString = "Server=10.224.143.244;Port=3306;Database=mex_mes;User=root;Password=secret;";

        var result = SecretProtector.UnprotectConfiguredValue(connectionString);

        Assert.Equal(connectionString, result);
    }
}

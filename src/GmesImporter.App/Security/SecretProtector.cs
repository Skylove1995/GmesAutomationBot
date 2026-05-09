using System.Security.Cryptography;
using System.Text;

namespace GmesImporter.App.Security;

public static class SecretProtector
{
    public const string Prefix = "dpapi:";

    public static string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string UnprotectConfiguredValue(string configuredValue)
    {
        if (!configuredValue.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return configuredValue;
        }

        var protectedBytes = Convert.FromBase64String(configuredValue[Prefix.Length..]);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}

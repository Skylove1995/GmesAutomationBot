using System.Net.Sockets;
using GmesImporter.App.Security;
using MySqlConnector;

namespace GmesImporter.App.Database;

public static class DatabaseDiagnostics
{
    public static async Task<int> RunAsync(string configuredConnectionString, CancellationToken cancellationToken)
    {
        var connectionString = SecretProtector.UnprotectConfiguredValue(configuredConnectionString);
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var port = builder.Port == 0 ? 3306u : builder.Port;

        Console.WriteLine("Database diagnostic");
        Console.WriteLine($"  Server   : {builder.Server}");
        Console.WriteLine($"  Port     : {port}");
        Console.WriteLine($"  Database : {builder.Database}");
        Console.WriteLine($"  User     : {builder.UserID}");
        Console.WriteLine($"  Timeout  : {builder.ConnectionTimeout}s");
        Console.WriteLine("  Password : [hidden]");

        var tcpSucceeded = await TestTcpAsync(builder.Server, (int)port, TimeSpan.FromSeconds(5), cancellationToken);
        Console.WriteLine($"  TCP      : {(tcpSucceeded ? "OK" : "FAILED")}");
        if (!tcpSucceeded)
        {
            Console.WriteLine("TCP failed before MySQL login. Check IP, port, firewall, VPN, or whether MySQL allows remote access.");
            return 2;
        }

        try
        {
            await MySqlProductionRepository.TestConnectionAsync(connectionString, cancellationToken);
            Console.WriteLine("  MySQL    : OK");
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"  MySQL    : FAILED - {exception.Message}");
            return 3;
        }
    }

    private static async Task<bool> TestTcpAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
        var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeout, cancellationToken));
        if (completedTask != connectTask)
        {
            return false;
        }

        await connectTask;
        return client.Connected;
    }
}

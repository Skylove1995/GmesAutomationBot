using GmesImporter.Core.Importing;
using GmesImporter.Core.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace GmesImporter.App.Database;

public sealed class MySqlProductionRepository : IProductionRepository, IAsyncDisposable
{
    private readonly MySqlConnection _connection;
    private readonly MySqlTransaction _transaction;
    private readonly ILogger<MySqlProductionRepository> _logger;

    private MySqlProductionRepository(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ILogger<MySqlProductionRepository> logger)
    {
        _connection = connection;
        _transaction = transaction;
        _logger = logger;
    }

    public static async Task<MySqlProductionRepository> OpenAsync(
        string connectionString,
        ILogger<MySqlProductionRepository> logger,
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return new MySqlProductionRepository(connection, transaction, logger);
    }

    public static async Task TestConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task<ExistingProductionRecord?> FindByPidAsync(string pid, CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText =
            """
            SELECT PID, Date
            FROM tb_gmes_production
            WHERE PID = @pid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@pid", pid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExistingProductionRecord(
            reader.GetString("PID"),
            reader.IsDBNull(reader.GetOrdinal("Date")) ? null : reader.GetDateTime("Date"));
    }

    public async Task InsertAsync(ProductionRecord record, CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText =
            """
            INSERT INTO tb_gmes_production (WO, EBR, PID, Date)
            VALUES (@wo, @ebr, @pid, @date);
            """;
        AddProductionParameters(command, record);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await _transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Database transaction committed.");
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        await _transaction.RollbackAsync(cancellationToken);
        _logger.LogInformation("Database transaction rolled back.");
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static void AddProductionParameters(MySqlCommand command, ProductionRecord record)
    {
        command.Parameters.AddWithValue("@wo", record.WorkOrder);
        command.Parameters.AddWithValue("@ebr", record.Ebr);
        command.Parameters.AddWithValue("@pid", record.Pid);
        command.Parameters.AddWithValue("@date", (object?)record.Date ?? DBNull.Value);
    }

    public async Task<int> UpsertBatchAsync(IReadOnlyList<ProductionRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return 0;

        await using var command = _connection.CreateCommand();
        command.Transaction = _transaction;

        var valueStrings = new List<string>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            valueStrings.Add($"(@wo{i}, @ebr{i}, @pid{i}, @date{i})");
            var record = batch[i];
            command.Parameters.AddWithValue($"@wo{i}", record.WorkOrder);
            command.Parameters.AddWithValue($"@ebr{i}", record.Ebr);
            command.Parameters.AddWithValue($"@pid{i}", record.Pid);
            command.Parameters.AddWithValue($"@date{i}", (object?)record.Date ?? DBNull.Value);
        }

        command.CommandText =
            $"""
             INSERT INTO tb_gmes_production (WO, EBR, PID, Date)
             VALUES {string.Join(", ", valueStrings)}
             ON DUPLICATE KEY UPDATE
                 WO   = VALUES(WO),
                 EBR  = VALUES(EBR),
                 Date = VALUES(Date);
             """;

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

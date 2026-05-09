using GmesImporter.Core.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace GmesImporter.App.Database;

public sealed class MySqlStencilRepository : IAsyncDisposable
{
    private readonly MySqlConnection _connection;
    private readonly MySqlTransaction _transaction;
    private readonly ILogger<MySqlStencilRepository> _logger;

    private MySqlStencilRepository(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ILogger<MySqlStencilRepository> logger)
    {
        _connection = connection;
        _transaction = transaction;
        _logger = logger;
    }

    public static async Task<MySqlStencilRepository> OpenAsync(
        string connectionString,
        ILogger<MySqlStencilRepository> logger,
        CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return new MySqlStencilRepository(connection, transaction, logger);
    }

    public async Task<int> UpsertBatchAsync(IReadOnlyList<StencilRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return 0;

        await using var command = _connection.CreateCommand();
        command.Transaction = _transaction;

        var valueStrings = new List<string>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            valueStrings.Add($"(@id{i}, @count{i}, @date{i})");
            var record = batch[i];
            command.Parameters.AddWithValue($"@id{i}", record.StencilId);
            command.Parameters.AddWithValue($"@count{i}", record.TotalCount);
            command.Parameters.AddWithValue($"@date{i}", record.DateReceive);
        }

        // According to requirement: Check by STENCIL_ID, if exists -> replace TOTAL_COUNT and DATE_RECEIVE. If not exists -> Insert
        command.CommandText =
            $"""
             INSERT INTO tb_spec_stencil (STENCIL_ID, TOTAL_COUNT, DATE_RECEIVE)
             VALUES {string.Join(", ", valueStrings)}
             ON DUPLICATE KEY UPDATE
                 TOTAL_COUNT = VALUES(TOTAL_COUNT),
                 DATE_RECEIVE = VALUES(DATE_RECEIVE);
             """;

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await _transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Stencil Database transaction committed.");
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        await _transaction.RollbackAsync(cancellationToken);
        _logger.LogInformation("Stencil Database transaction rolled back.");
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
